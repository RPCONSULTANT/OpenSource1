using System.Globalization;
using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Escritor único del libro contable (Task 5.5). Toda la escritura va con Dapper sobre la conexión y la transacción de
/// <see cref="IDbSession"/> (la del llamador: nunca abre ni confirma una propia). Solo hace <c>INSERT</c>: el libro es
/// append-only (trigger <c>libro_inventario_append_only()</c>).
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>Validación en memoria (cabecera, líneas y cuadre <c>SUM(Importe) = 0</c>): devuelve TODOS los errores sin tocar la base.</item>
/// <item>Cuentas: existen (no borradas), son de Posteo y no están bloqueadas. Se leen <c>FOR SHARE</c> (en orden de Id). El
/// borrado de una cuenta y su salida de Posteo toman antes la fila <c>FOR UPDATE</c> (<c>ICuentaContableUsoService.BloquearAsync</c>)
/// y después evalúan el uso: o esperan a este commit (y ven sus movimientos → 409), o este registro espera a su commit y ve la
/// cuenta ya borrada / fuera de Posteo (y la rechaza). Un simple <c>UPDATE</c> que solo la bloquee (sin pasar por la guarda) sí
/// esperaría igual, por el conflicto FOR SHARE/UPDATE.</item>
/// <item>Bloqueo del libro contable (<see cref="BloqueoLibroSql"/>, advisory lock de transacción con clave constante): serializa
/// a TODOS los escritores del libro hasta el commit/rollback, sea cual sea la serie configurada para <c>AsientoContable</c> o la
/// línea vigente (una reasignación de la serie o un cambio de línea por fecha no abren dos escritores a la vez).</item>
/// <item>Número de la serie configurada para el tipo <c>AsientoContable</c> (<c>FOR SHARE</c> de la serie y <c>FOR UPDATE</c> de
/// su línea). Un fallo anterior no lo consume; uno posterior lo deshace la transacción.</item>
/// <item>Ids de los movimientos reservados de su secuencia de identidad, registro con su rango y movimientos con esos ids
/// (<c>OVERRIDING SYSTEM VALUE</c>). Son contiguos DENTRO del registro porque todos los escritores están serializados por el
/// bloqueo del libro; se comprueba y, si no, excepción. Entre registros puede haber huecos inocuos (un rollback tras
/// <c>nextval</c> no devuelve los ids a la secuencia).</item>
/// <item>Red final: <c>SELECT COUNT/SUM</c> del registro recién escrito; si no cuadra, excepción (nunca un <c>Result</c>):
/// el llamador no llega a confirmar y su transacción se deshace entera.</item>
/// </list>
/// </remarks>
public sealed class RegistroContable(
    IDbSession session,
    IGeneradorNumeroDocumento generadorNumero,
    IUsuarioActual usuario) : IRegistroContable
{
    /// <summary>
    /// Advisory lock EXCLUSIVO de transacción del libro contable (spec no-series, ruling NSC2). Clave de texto con espacio de nombres
    /// propio (<c>libro-contable</c>), como <c>contab-costo</c> (<c>PosteoCostoInventario</c>), <c>almacen:{id}</c> y los Guid de
    /// productos (<c>BloqueoInventarioProducto</c>): no choca con ellas. Solo lo toma <see cref="RegistrarAsync"/>, siempre en el
    /// mismo punto del orden global (tras las cuentas, antes de la serie de asientos y su línea).
    /// </summary>
    public const string BloqueoLibroSql = "SELECT pg_advisory_xact_lock(hashtextextended('libro-contable', 0))";

    private const int LongitudDescripcion = 200;
    private const int LongitudClaveOrigen = 50;
    private const int LongitudNumeroDocumento = 20;
    private const int LongitudNumeroRegistro = 20;
    private const int LongitudCreatedBy = 100;

    /// <summary>Máximo absoluto de numeric(18,4).</summary>
    private const decimal ImporteMaximo = 99_999_999_999_999.9999m;

    public async Task<Result<AsientoRegistrado>> RegistrarAsync(AsientoContable asiento, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(asiento);

        if (!session.HayTransaccionActiva)
        {
            return Fallo(new Error(
                "contabilidad.sin_transaccion", "El registro de asientos contables requiere una transacción activa."));
        }

        // 1. Validación en memoria: nada toca la base todavía.
        var errores = ValidarEnMemoria(asiento);
        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        await session.EnsureOpenAsync(ct);
        var tx = session.CurrentTransaction;

        // 2. Cuentas (FOR SHARE, orden único por Id).
        var ids = asiento.Lineas.Select(x => x.CuentaContableId).Distinct().Order().ToArray();
        var cuentas = (await session.Connection.QueryAsync<CuentaFila>(new CommandDefinition(
            """
            SELECT "Id", "Numero", "TipoCuenta", "Bloqueada" FROM "CuentasContables"
            WHERE "Id" = ANY(@Ids) AND "IsDeleted" = false
            ORDER BY "Id"
            FOR SHARE
            """,
            new { Ids = ids }, tx, cancellationToken: ct))).ToDictionary(x => x.Id);

        for (var i = 0; i < asiento.Lineas.Count; i++)
        {
            var campo = $"Lineas[{i}].CuentaContableId";
            if (!cuentas.TryGetValue(asiento.Lineas[i].CuentaContableId, out var cuenta))
            {
                errores.Add(new Error("contabilidad.cuenta_invalida", $"Línea {i + 1}: la cuenta contable no existe.", campo));
            }
            else if (cuenta.TipoCuenta != TipoCuentaContable.Posteo)
            {
                errores.Add(new Error(
                    "contabilidad.cuenta_no_posteo",
                    $"Línea {i + 1}: la cuenta {cuenta.Numero} no es de Posteo y no admite movimientos.", campo));
            }
            else if (cuenta.Bloqueada)
            {
                errores.Add(new Error("contabilidad.cuenta_bloqueada", $"Línea {i + 1}: la cuenta {cuenta.Numero} está bloqueada.", campo));
            }
        }

        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // 3. Bloqueo del libro (serializa a todos los escritores, independiente de la serie y su línea) y número de registro
        //    (serie configurada para AsientoContable). La fecha elige la línea de serie vigente: la de creación del registro, como
        //    en el registro de diarios de inventario (la fecha contable del asiento puede ser anterior a la primera línea).
        await session.Connection.ExecuteAsync(new CommandDefinition(BloqueoLibroSql, transaction: tx, cancellationToken: ct));
        var numero = await generadorNumero.SiguientePorTipoAsync(
            TipoDocumentoSerie.AsientoContable, DateOnly.FromDateTime(DateTime.UtcNow), ct);
        if (!numero.TryObtenerValor(out var generado))
        {
            return Result<AsientoRegistrado>.Fallo(numero);
        }

        var numeroRegistro = generado.Numero;

        if (numeroRegistro.Length > LongitudNumeroRegistro)
        {
            return Fallo(new Error(
                "contabilidad.serie_invalida",
                $"El número de registro generado supera los {LongitudNumeroRegistro} caracteres."));
        }

        // 4. Ids de los movimientos, registro y movimientos.
        var movimientoIds = (await session.Connection.QueryAsync<long>(new CommandDefinition(
            """
            SELECT nextval(pg_get_serial_sequence('"MovimientosContables"', 'Id'))
            FROM generate_series(1, @Cantidad)
            """,
            new { Cantidad = asiento.Lineas.Count }, tx, cancellationToken: ct))).Order().ToArray();
        if (movimientoIds.Length != asiento.Lineas.Count || movimientoIds[^1] - movimientoIds[0] + 1 != asiento.Lineas.Count)
        {
            // Solo posible si alguien consume la secuencia sin tomar el bloqueo del libro (otro escritor del libro).
            throw new InvalidOperationException(
                $"Los ids reservados para el registro contable {numeroRegistro} no son contiguos " +
                $"({movimientoIds[0]}..{movimientoIds[^1]} para {asiento.Lineas.Count} líneas).");
        }

        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();

        var registroId = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "RegistrosContables" (
                "NumeroRegistro", "DesdeMovimiento", "HastaMovimiento", "FechaCreacion", "CreadoPor", "UsuarioId",
                "TipoOrigen", "ClaveOrigen")
            VALUES (@NumeroRegistro, @Desde, @Hasta, @Ahora, @CreadoPor, @UsuarioId, @TipoOrigen, @ClaveOrigen)
            RETURNING "Id"
            """,
            new
            {
                NumeroRegistro = numeroRegistro,
                Desde = movimientoIds[0],
                Hasta = movimientoIds[^1],
                Ahora = ahora,
                CreadoPor = creadoPor,
                UsuarioId = usuario.Id,
                asiento.TipoOrigen,
                asiento.ClaveOrigen,
            },
            tx, cancellationToken: ct));

        var filas = asiento.Lineas.Select((linea, i) => new
        {
            Id = movimientoIds[i],
            linea.CuentaContableId,
            NumeroCuenta = cuentas[linea.CuentaContableId].Numero,
            asiento.FechaRegistro,
            asiento.FechaDocumento,
            asiento.TipoDocumento,
            asiento.NumeroDocumento,
            Descripcion = string.IsNullOrWhiteSpace(linea.Descripcion) ? asiento.Descripcion : linea.Descripcion,
            linea.Importe,
            Debito = Math.Max(linea.Importe, 0m),
            Credito = Math.Max(-linea.Importe, 0m),
            RegistroContableId = registroId,
            linea.SocioNegocioId,
            linea.ProductoId,
            linea.GrupoNegocioId,
            linea.GrupoProductoId,
            linea.GrupoIvaNegocioId,
            linea.GrupoIvaProductoId,
            asiento.TipoOrigen,
            asiento.ClaveOrigen,
            CreatedAtUtc = ahora,
            CreatedBy = creadoPor,
            UsuarioId = usuario.Id,
        });

        await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO "MovimientosContables" (
                "Id", "CuentaContableId", "NumeroCuenta", "FechaRegistro", "FechaDocumento", "TipoDocumento", "NumeroDocumento",
                "Descripcion", "Importe", "Debito", "Credito", "RegistroContableId", "SocioNegocioId", "ProductoId",
                "GrupoNegocioId", "GrupoProductoId", "GrupoIvaNegocioId", "GrupoIvaProductoId", "TipoOrigen", "ClaveOrigen",
                "CreatedAtUtc", "CreatedBy", "UsuarioId")
            OVERRIDING SYSTEM VALUE
            VALUES (
                @Id, @CuentaContableId, @NumeroCuenta, @FechaRegistro, @FechaDocumento, @TipoDocumento, @NumeroDocumento,
                @Descripcion, @Importe, @Debito, @Credito, @RegistroContableId, @SocioNegocioId, @ProductoId,
                @GrupoNegocioId, @GrupoProductoId, @GrupoIvaNegocioId, @GrupoIvaProductoId, @TipoOrigen, @ClaveOrigen,
                @CreatedAtUtc, @CreatedBy, @UsuarioId)
            """,
            filas, tx, cancellationToken: ct));

        // 5. Invariante dura (spec 5.5): el registro escrito cuadra a 0. Si no, excepción -> el llamador no confirma.
        var (cantidad, suma) = await session.Connection.QuerySingleAsync<(long Cantidad, decimal Suma)>(new CommandDefinition(
            """
            SELECT COUNT(*), COALESCE(SUM("Importe"), 0) FROM "MovimientosContables" WHERE "RegistroContableId" = @Id
            """,
            new { Id = registroId }, tx, cancellationToken: ct));
        if (suma != 0m || cantidad != asiento.Lineas.Count)
        {
            throw new InvalidOperationException(
                $"Registro contable {numeroRegistro} descuadrado tras insertar: suma {Formato(suma)} en {cantidad} movimientos " +
                $"(esperados {asiento.Lineas.Count}). La transacción no debe confirmarse.");
        }

        return Result<AsientoRegistrado>.Exito(
            new AsientoRegistrado(registroId, numeroRegistro, movimientoIds[0], movimientoIds[^1]));
    }

    /// <summary>Reglas que no necesitan la base. Devuelve todos los errores encontrados.</summary>
    private static List<Error> ValidarEnMemoria(AsientoContable asiento)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(asiento.Descripcion) || asiento.Descripcion.Length > LongitudDescripcion)
        {
            errores.Add(new Error(
                "contabilidad.descripcion_invalida",
                $"La descripción del asiento es obligatoria y admite como máximo {LongitudDescripcion} caracteres.", "Descripcion"));
        }

        if (string.IsNullOrWhiteSpace(asiento.ClaveOrigen) || asiento.ClaveOrigen.Length > LongitudClaveOrigen)
        {
            errores.Add(new Error(
                "contabilidad.clave_origen_invalida",
                $"La clave de origen es obligatoria y admite como máximo {LongitudClaveOrigen} caracteres.", "ClaveOrigen"));
        }

        if (asiento.NumeroDocumento is { Length: > LongitudNumeroDocumento })
        {
            errores.Add(new Error(
                "contabilidad.numero_documento_invalido",
                $"El número de documento admite como máximo {LongitudNumeroDocumento} caracteres.", "NumeroDocumento"));
        }

        if (asiento.Lineas is null || asiento.Lineas.Count == 0)
        {
            errores.Add(new Error("contabilidad.asiento_vacio", "El asiento no tiene líneas.", "Lineas"));
            return errores;
        }

        var lineasValidas = true;
        for (var i = 0; i < asiento.Lineas.Count; i++)
        {
            var linea = asiento.Lineas[i];
            if (linea.Importe == 0m || Math.Abs(linea.Importe) > ImporteMaximo || decimal.Round(linea.Importe, 4) != linea.Importe)
            {
                lineasValidas = false;
                errores.Add(new Error(
                    "contabilidad.importe_invalido",
                    $"Línea {i + 1}: el importe debe ser distinto de cero, menor que 1e14 en valor absoluto y tener como máximo 4 decimales.",
                    $"Lineas[{i}].Importe"));
            }

            if (linea.Descripcion is { Length: > LongitudDescripcion })
            {
                errores.Add(new Error(
                    "contabilidad.descripcion_invalida",
                    $"Línea {i + 1}: la descripción admite como máximo {LongitudDescripcion} caracteres.", $"Lineas[{i}].Descripcion"));
            }
        }

        // El cuadre solo se evalúa con importes válidos (con uno fuera de rango la suma podría desbordar).
        if (lineasValidas)
        {
            var debitos = asiento.Lineas.Where(x => x.Importe > 0).Sum(x => x.Importe);
            var creditos = -asiento.Lineas.Where(x => x.Importe < 0).Sum(x => x.Importe);
            var descuadre = debitos - creditos;
            if (descuadre != 0m)
            {
                errores.Add(new Error(
                    "contabilidad.asiento_descuadrado",
                    $"El asiento está descuadrado en {Formato(descuadre)} (débitos {Formato(debitos)}, créditos {Formato(creditos)}).",
                    "Lineas"));
            }
        }

        return errores;
    }

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreatedBy ? nombre : nombre[..LongitudCreatedBy];
    }

    private static string Formato(decimal valor) => valor.ToString("0.0000", CultureInfo.InvariantCulture);

    private static Result<AsientoRegistrado> Fallo(params Error[] errores) => Result<AsientoRegistrado>.Fallo(errores);

    private sealed record CuentaFila(Guid Id, string Numero, TipoCuentaContable TipoCuenta, bool Bloqueada);
}
