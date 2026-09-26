using Dapper;
using OpenSource1.Application.Data;
using OpenSource1.Application.Services.Clientes;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Infrastructure.Services.Clientes;

/// <summary>
/// Escritor único del libro de clientes (Task 6.4). Dapper sobre la conexión y la transacción de <see cref="IDbSession"/> (la del
/// llamador: nunca abre ni confirma una propia). Solo <c>INSERT</c>: las dos tablas son append-only (trigger
/// <c>libro_inventario_append_only()</c>).
/// </summary>
public sealed class RegistroMovimientosCliente(IDbSession session, IUsuarioActual usuario) : IRegistroMovimientosCliente
{
    private const int LongitudNumeroDocumento = 20;
    private const int LongitudDescripcion = 200;
    private const int LongitudClaveOrigen = 50;
    private const int LongitudCreatedBy = 100;

    /// <summary>Máximo absoluto de numeric(18,4).</summary>
    private const decimal ImporteMaximo = 99_999_999_999_999.9999m;

    public async Task<Result<MovimientoClienteRegistrado>> RegistrarAsync(MovimientoClienteSolicitud solicitud, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);

        if (!session.HayTransaccionActiva)
        {
            return Fallo(new Error("clientes.sin_transaccion", "El registro de movimientos de cliente requiere una transacción activa."));
        }

        var errores = Validar(solicitud);
        if (errores.Count > 0)
        {
            return Result<MovimientoClienteRegistrado>.Fallo([.. errores]);
        }

        await session.EnsureOpenAsync(ct);
        var tx = session.CurrentTransaction;
        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();

        var movimientoId = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "MovimientosCliente" (
                "SocioNegocioId", "FechaRegistro", "FechaDocumento", "FechaVencimiento", "TipoDocumento", "NumeroDocumento",
                "Descripcion", "ImporteOriginal", "GrupoClienteContableId", "CuentaCxCId", "TipoOrigen", "ClaveOrigen",
                "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @SocioNegocioId, @FechaRegistro, @FechaDocumento, @FechaVencimiento, @TipoDocumento, @NumeroDocumento,
                @Descripcion, @ImporteOriginal, @GrupoClienteContableId, @CuentaCxCId, @TipoOrigen, @ClaveOrigen,
                @CreatedAtUtc, @CreatedBy, @UsuarioId)
            RETURNING "Id"
            """,
            new
            {
                solicitud.SocioNegocioId,
                solicitud.FechaRegistro,
                solicitud.FechaDocumento,
                solicitud.FechaVencimiento,
                solicitud.TipoDocumento,
                solicitud.NumeroDocumento,
                solicitud.Descripcion,
                solicitud.ImporteOriginal,
                solicitud.GrupoClienteContableId,
                solicitud.CuentaCxCId,
                solicitud.TipoOrigen,
                solicitud.ClaveOrigen,
                CreatedAtUtc = ahora,
                CreatedBy = creadoPor,
                UsuarioId = usuario.Id,
            },
            tx, cancellationToken: ct));

        var detalleId = await session.Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            """
            INSERT INTO "MovimientosClienteDetalle" (
                "MovimientoClienteId", "TipoMovimiento", "Importe", "FechaRegistro", "MovimientoClienteAplicadoId", "TipoOrigen",
                "ClaveOrigen", "CreatedAtUtc", "CreatedBy", "UsuarioId")
            VALUES (
                @MovimientoClienteId, @TipoMovimiento, @Importe, @FechaRegistro, NULL, @TipoOrigen,
                @ClaveOrigen, @CreatedAtUtc, @CreatedBy, @UsuarioId)
            RETURNING "Id"
            """,
            new
            {
                MovimientoClienteId = movimientoId,
                TipoMovimiento = solicitud.TipoDocumento == TipoDocumentoCliente.Pago
                    ? TipoDetalleCliente.Pago
                    : TipoDetalleCliente.ImporteInicial,
                Importe = solicitud.ImporteOriginal,
                solicitud.FechaRegistro,
                solicitud.TipoOrigen,
                solicitud.ClaveOrigen,
                CreatedAtUtc = ahora,
                CreatedBy = creadoPor,
                UsuarioId = usuario.Id,
            },
            tx, cancellationToken: ct));

        return Result<MovimientoClienteRegistrado>.Exito(new MovimientoClienteRegistrado(movimientoId, detalleId));
    }

    private static List<Error> Validar(MovimientoClienteSolicitud s)
    {
        var errores = new List<Error>();

        if (!Enum.IsDefined(s.TipoDocumento))
        {
            errores.Add(new Error("clientes.tipo_documento_invalido", "El tipo de documento del movimiento de cliente no es válido.", "TipoDocumento"));
        }

        if (string.IsNullOrWhiteSpace(s.NumeroDocumento) || s.NumeroDocumento.Length > LongitudNumeroDocumento)
        {
            errores.Add(new Error(
                "clientes.numero_documento_invalido",
                $"El número de documento es obligatorio y admite como máximo {LongitudNumeroDocumento} caracteres.", "NumeroDocumento"));
        }

        if (s.Descripcion is { Length: > LongitudDescripcion })
        {
            errores.Add(new Error(
                "clientes.descripcion_invalida", $"La descripción admite como máximo {LongitudDescripcion} caracteres.", "Descripcion"));
        }

        if (string.IsNullOrWhiteSpace(s.ClaveOrigen) || s.ClaveOrigen.Length > LongitudClaveOrigen)
        {
            errores.Add(new Error(
                "clientes.clave_origen_invalida",
                $"La clave de origen es obligatoria y admite como máximo {LongitudClaveOrigen} caracteres.", "ClaveOrigen"));
        }

        if (s.ImporteOriginal == 0m || Math.Abs(s.ImporteOriginal) > ImporteMaximo || decimal.Round(s.ImporteOriginal, 4) != s.ImporteOriginal)
        {
            errores.Add(new Error(
                "clientes.importe_invalido",
                "El importe debe ser distinto de cero, menor que 1e14 en valor absoluto y tener como máximo 4 decimales.", "ImporteOriginal"));
        }
        else if (s.TipoDocumento == TipoDocumentoCliente.Factura && s.ImporteOriginal < 0m)
        {
            errores.Add(new Error("clientes.importe_invalido", "El importe de una factura debe ser positivo.", "ImporteOriginal"));
        }
        else if (s.TipoDocumento is TipoDocumentoCliente.Pago or TipoDocumentoCliente.NotaCredito && s.ImporteOriginal > 0m)
        {
            errores.Add(new Error("clientes.importe_invalido", "El importe de un pago o nota de crédito debe ser negativo.", "ImporteOriginal"));
        }

        if (s.FechaRegistro == default || s.FechaDocumento == default || s.FechaVencimiento == default)
        {
            errores.Add(new Error("clientes.fecha_invalida", "Las fechas del movimiento de cliente son obligatorias.", "FechaRegistro"));
        }

        return errores;
    }

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreatedBy ? nombre : nombre[..LongitudCreatedBy];
    }

    private static Result<MovimientoClienteRegistrado> Fallo(params Error[] errores) => Result<MovimientoClienteRegistrado>.Fallo(errores);
}
