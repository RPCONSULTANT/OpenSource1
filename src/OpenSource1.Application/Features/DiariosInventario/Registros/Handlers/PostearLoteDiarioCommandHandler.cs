using MediatR;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lineas;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Registros.Handlers;

/// <summary>
/// Registro atómico de un lote de diario (Task 4.3). Todo ocurre en UNA transacción de <see cref="IUnitOfWork"/> (EF y
/// Dapper comparten la conexión): un <c>Result</c> fallido o una excepción deshacen TODO, incluido el número de la serie,
/// que así nunca deja huecos.
/// <list type="number">
/// <item>Bloqueo de la fila del lote y de sus líneas (<c>FOR UPDATE</c>): serializa dos registros del mismo lote y las
/// altas/ediciones de línea concurrentes.</item>
/// <item>Bloqueo de todos los productos del lote (orden único por Guid, <see cref="IRegistroMovimientosInventario.BloquearProductosAsync"/>).
/// Se toma ANTES de la prevalidación (el brief lo pone después) para que la unidad base y el factor que se revalidan no
/// puedan cambiar hasta el commit: cambiar la unidad base toma el mismo bloqueo.</item>
/// <item>Prevalidación de todas las líneas con las reglas de captura contra el estado ACTUAL, más el factor congelado
/// (<c>diario.factor_cambiado</c>); devuelve TODOS los errores, sin escribir nada.</item>
/// <item>Número de registro de la serie del lote (o de la plantilla).</item>
/// <item>Movimientos en el orden de las desviaciones (fecha, entradas antes que salidas, número de línea).</item>
/// <item>Invariante de reclasificación (salida + entrada = 0), <c>RegistroDiario</c> y borrado lógico de las líneas.</item>
/// </list>
/// <para>
/// Orden GLOBAL de locks (ver también el XML doc de <c>BloqueoInventarioProducto</c>/<c>BloqueoInventarioAlmacen</c>):
/// lote -&gt; líneas -&gt; productos (ordenados) -&gt; línea de serie -&gt; almacenes (compartidos, uno por
/// <see cref="MovimientoInventarioSolicitud"/> dentro del paso 5). El <c>FOR UPDATE</c> de la línea de serie
/// (<see cref="IGeneradorNumeroDocumento.SiguienteAsync"/>) serializa, además, GLOBALMENTE todos los registros que
/// usan esa misma serie: por eso quitar el bloqueo de productos del paso 2 no haría fallar por interbloqueo dos
/// posteos concurrentes que no comparten producto (ya los serializa la serie); ese bloqueo de productos sigue siendo
/// necesario para el interbloqueo A/B-B/A DENTRO de un mismo posteo y para no colarse con otros escritores del
/// producto (borrarlo, cambiar su unidad base).
/// </para>
/// </summary>
public sealed class PostearLoteDiarioCommandHandler(
    IUnitOfWork unitOfWork,
    ILoteDiarioBloqueoService loteBloqueo,
    IRegistroLoteDiarioDatos datos,
    IConversionUnidadMedidaService conversion,
    IRegistroMovimientosInventario registroMovimientos,
    IGeneradorNumeroDocumento generadorNumero,
    IUsuarioActual usuario)
    : IRequestHandler<PostearLoteDiarioCommand, Result<ResultadoRegistroLote>>
{
    private const int LongitudNumeroRegistro = 20;
    private const int LongitudCreadoPor = 100;

    public async Task<Result<ResultadoRegistroLote>> Handle(PostearLoteDiarioCommand request, CancellationToken cancellationToken)
    {
        // Este handler garantiza SU PROPIA atomicidad (ver el XML doc de PostearLoteDiarioCommand): invocarlo dentro
        // de una transacción ya abierta haría que BeginTransactionAsync se uniera a un ámbito anidado que no hace
        // nada al salir, así que un "return Fallo" de abajo no deshace nada y el CommitAsync de más abajo confirmaría
        // la transacción EXTERNA. Mejor fallar alto y claro que dejarlo pasar en silencio.
        if (unitOfWork.HayTransaccionActiva)
        {
            throw new InvalidOperationException(
                "PostearLoteDiario debe ejecutarse fuera de otra transacción: garantiza su propia atomicidad.");
        }

        // Sin CommitAsync, salir del "await using" deshace la transacción (y libera todos los bloqueos): cada "return" de
        // fallo de abajo deja la base exactamente como estaba.
        await using var transaccion = await unitOfWork.BeginTransactionAsync(cancellationToken);

        // 1. Lote (FOR UPDATE de su fila) y sus líneas (FOR UPDATE de cada una).
        var loteBloqueado = await loteBloqueo.BloquearYObtenerEstadoAsync(request.LoteDiarioId, cancellationToken);
        if (loteBloqueado is null)
        {
            return Fallo(new Error("diario.no_encontrado", "No se encontró el lote de diario solicitado.", "LoteDiarioId"));
        }

        if (loteBloqueado.Value)
        {
            return Fallo(new Error("diario.lote_bloqueado", "El lote está bloqueado.", "LoteDiarioId"));
        }

        var lineas = await datos.BloquearLineasAsync(request.LoteDiarioId, cancellationToken);
        if (lineas.Count == 0)
        {
            return Fallo(new Error("diario.lote_vacio", "El lote no tiene líneas que registrar.", "LoteDiarioId"));
        }

        var lote = await unitOfWork.Repository<LoteDiario>().FirstOrDefaultAsync(
            x => x.Id == request.LoteDiarioId, cancellationToken: cancellationToken);
        var plantilla = lote is null
            ? null
            : await unitOfWork.Repository<PlantillaDiario>().FirstOrDefaultAsync(
                x => x.Id == lote.PlantillaDiarioId, cancellationToken: cancellationToken);
        if (lote is null || plantilla is null)
        {
            // Imposible con la fila del lote bloqueada (y la plantilla es sembrada, sin CRUD).
            return Fallo(new Error("diario.no_encontrado", "No se encontró el lote de diario solicitado.", "LoteDiarioId"));
        }

        // 2. Bloqueo de todos los productos del lote antes del primer RegistrarAsync (evita el interbloqueo A,B / B,A).
        await registroMovimientos.BloquearProductosAsync(lineas.Select(x => x.ProductoId), cancellationToken);

        // 3. Prevalidación completa: todos los errores de todas las líneas en una sola respuesta.
        var errores = new List<Error>();
        foreach (var linea in lineas)
        {
            if (await PrevalidarAsync(request.LoteDiarioId, linea, cancellationToken) is { } error)
            {
                errores.Add(error);
            }
        }

        if (errores.Count > 0)
        {
            return Fallo([.. errores]);
        }

        // 4. Número de registro (FOR UPDATE de la línea de serie; se deshace con el resto si algo falla después).
        var serieId = lote.SerieId ?? plantilla.SerieId;
        var serie = await unitOfWork.Repository<Serie>().FirstOrDefaultAsync(x => x.Id == serieId, cancellationToken: cancellationToken);
        if (serie is null)
        {
            return Fallo(new Error("diario.serie_invalida", "La serie de numeración del lote no existe.", "SerieId"));
        }

        var numero = await generadorNumero.SiguienteAsync(serie.Codigo, DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
        if (!numero.TryObtenerValor(out var numeroRegistro))
        {
            return Fallo(numero);
        }

        if (numeroRegistro.Length > LongitudNumeroRegistro)
        {
            return Fallo(new Error(
                "diario.serie_invalida",
                $"El número de registro generado supera los {LongitudNumeroRegistro} caracteres.", "SerieId"));
        }

        // 5. Movimientos, en el orden de las desviaciones de la Fase 4: una salida puede consumir una entrada del mismo día
        //    del mismo lote aunque la entrada esté en una línea posterior.
        var movimientoProductoIds = new List<long>();
        foreach (var linea in OrdenDeRegistro(lineas))
        {
            if (await RegistrarLineaAsync(linea, numeroRegistro, movimientoProductoIds, cancellationToken) is { } fallo)
            {
                return Fallo(fallo);
            }
        }

        // 6. RegistroDiario (append-only) y borrado lógico de las líneas ya registradas.
        var ahora = DateTimeOffset.UtcNow;
        var creadoPor = CreadoPor();
        await datos.InsertarRegistroAsync(new RegistroDiario
        {
            NumeroRegistro = numeroRegistro,
            LoteDiarioId = request.LoteDiarioId,
            DesdeMovimientoProducto = movimientoProductoIds.Min(),
            HastaMovimientoProducto = movimientoProductoIds.Max(),
            Lineas = lineas.Count,
            FechaCreacion = ahora,
            CreadoPor = creadoPor,
            UsuarioId = usuario.Id,
        }, cancellationToken);

        var borradas = await datos.BorrarLineasAsync([.. lineas.Select(x => x.Id)], creadoPor, cancellationToken);
        if (borradas != lineas.Count)
        {
            // Imposible con las líneas bloqueadas FOR UPDATE: abortar antes que dejar líneas registradas vivas.
            throw new InvalidOperationException(
                $"Se esperaba borrar {lineas.Count} líneas del lote {request.LoteDiarioId} y se borraron {borradas}.");
        }

        // 7. Commit (SaveChanges no tiene nada pendiente: toda la escritura fue por Dapper sobre la misma transacción).
        await unitOfWork.CommitAsync(cancellationToken);

        return Result<ResultadoRegistroLote>.Exito(new ResultadoRegistroLote(
            numeroRegistro, movimientoProductoIds.Count, movimientoProductoIds.Min(), movimientoProductoIds.Max()));
    }

    /// <summary>
    /// Orden de registro (desviación de la Fase 4): <c>FechaRegistro</c>, luego entradas (ajuste positivo) antes que
    /// salidas (ajuste negativo y transferencia, que empieza por la salida del origen), luego <c>NumeroLinea</c>.
    /// </summary>
    internal static IEnumerable<LineaDiarioARegistrar> OrdenDeRegistro(IEnumerable<LineaDiarioARegistrar> lineas) =>
        lineas
            .OrderBy(x => x.FechaRegistro)
            .ThenBy(x => x.TipoMovimiento == TipoMovimientoInventario.AjustePositivo ? 0 : 1)
            .ThenBy(x => x.NumeroLinea);

    /// <summary>Reglas de captura contra el estado actual + factor congelado. Devuelve el primer error de la línea o null.</summary>
    private async Task<Error?> PrevalidarAsync(Guid loteDiarioId, LineaDiarioARegistrar linea, CancellationToken cancellationToken)
    {
        var datosLinea = new LineaDiarioDatos(
            loteDiarioId, linea.FechaRegistro, linea.FechaDocumento, linea.NumeroDocumento, linea.TipoMovimiento,
            linea.ProductoId, linea.AlmacenId, linea.AlmacenDestinoId, linea.UnidadMedidaId, linea.Cantidad,
            linea.CostoUnitario, linea.Descripcion);

        var calculo = await LineaDiarioReglas.ValidarYCalcularAsync(unitOfWork, conversion, datosLinea, cancellationToken);
        if (!calculo.TryObtenerValor(out var valor))
        {
            return DeLinea(linea, calculo.Errores[0]);
        }

        if (valor.CantidadPorUnidadMedida != linea.CantidadPorUnidadMedida)
        {
            return DeLinea(linea, new Error(
                "diario.factor_cambiado",
                $"El factor de conversión de la unidad cambió desde que se guardó la línea ({linea.CantidadPorUnidadMedida:0.######} " +
                $"-> {valor.CantidadPorUnidadMedida:0.######}); vuelva a guardar la línea.",
                "UnidadMedidaId"));
        }

        return null;
    }

    /// <summary>Escribe los movimientos de una línea. Devuelve el error (ya con el campo de la línea) o null.</summary>
    private async Task<Error?> RegistrarLineaAsync(
        LineaDiarioARegistrar linea, string numeroRegistro, List<long> movimientoProductoIds, CancellationToken cancellationToken)
    {
        var numeroDocumento = linea.NumeroDocumento ?? numeroRegistro;
        var esEntrada = linea.TipoMovimiento == TipoMovimientoInventario.AjustePositivo;

        var principal = new MovimientoInventarioSolicitud(
            linea.ProductoId,
            linea.AlmacenId,
            linea.TipoMovimiento,
            linea.Cantidad,
            EsEntrada: esEntrada,
            linea.UnidadMedidaId,
            esEntrada ? linea.CostoUnitario : null,
            linea.FechaRegistro,
            linea.FechaDocumento,
            TipoDocumentoInventario.RegistroDiario,
            numeroDocumento,
            linea.NumeroLinea,
            TipoOrigenMovimiento.Diario,
            numeroRegistro);

        var resultado = await registroMovimientos.RegistrarAsync(principal, cancellationToken);
        if (!resultado.TryObtenerValor(out var salida))
        {
            return DeLinea(linea, resultado.Errores[0]);
        }

        movimientoProductoIds.Add(salida.MovimientoProductoId);

        if (linea.TipoMovimiento != TipoMovimientoInventario.Transferencia)
        {
            return null;
        }

        // Reclasificación: entrada gemela en el destino al costo EXACTO de la salida (-ImporteCosto / CantidadBase), con el
        // mismo NumeroLineaDocumento. RegistrarAsync toma el bloqueo compartido del almacén destino en esta segunda llamada.
        var costoSalida = -salida.ImporteCosto / salida.CantidadBase;
        var entrada = principal with
        {
            AlmacenId = linea.AlmacenDestinoId!.Value,
            EsEntrada = true,
            CostoUnitario = costoSalida,
        };

        var resultadoEntrada = await registroMovimientos.RegistrarAsync(entrada, cancellationToken);
        if (!resultadoEntrada.TryObtenerValor(out var entradaRegistrada))
        {
            var original = resultadoEntrada.Errores[0];
            // En la entrada gemela, "AlmacenId" es el almacén DESTINO de la línea.
            var campo = original.Campo == "AlmacenId" ? "AlmacenDestinoId" : original.Campo;
            return DeLinea(linea, original with { Campo = campo });
        }

        movimientoProductoIds.Add(entradaRegistrada.MovimientoProductoId);

        if (salida.ImporteCosto + entradaRegistrada.ImporteCosto != 0m)
        {
            // Invariante de la reclasificación (spec 4.2): nunca se confirma un traslado que cree o destruya valor.
            throw new InvalidOperationException(
                $"Reclasificación descuadrada en la línea {linea.NumeroLinea}: salida {salida.ImporteCosto}, " +
                $"entrada {entradaRegistrada.ImporteCosto}.");
        }

        return null;
    }

    private static Error DeLinea(LineaDiarioARegistrar linea, Error original) => new(
        original.Codigo,
        $"Línea {linea.NumeroLinea}: {original.Mensaje}",
        $"Lineas[{linea.NumeroLinea}].{original.Campo}");

    private string CreadoPor()
    {
        var nombre = string.IsNullOrWhiteSpace(usuario.Nombre) ? "system" : usuario.Nombre;
        return nombre.Length <= LongitudCreadoPor ? nombre : nombre[..LongitudCreadoPor];
    }

    private static Result<ResultadoRegistroLote> Fallo(params Error[] errores) => Result<ResultadoRegistroLote>.Fallo(errores);

    private static Result<ResultadoRegistroLote> Fallo(Result origen) => Result<ResultadoRegistroLote>.Fallo(origen);
}
