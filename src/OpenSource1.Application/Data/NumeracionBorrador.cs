using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Data;

/// <summary>Series y número de un borrador nuevo (<see cref="NumeracionBorrador.NumerarBorradorAsync"/>).</summary>
/// <param name="SerieBorradorId">Serie que dio <see cref="Numero"/> (la elegida o la configurada del tipo de borrador).</param>
/// <param name="SerieRegistroId">Serie con la que se numerará el documento al postear (la elegida o la configurada); solo validada.</param>
/// <param name="Numero">Número emitido por la serie de borradores (su aviso no se devuelve al usuario: NS4).</param>
public sealed record SeriesBorradorNumeradas(Guid SerieBorradorId, Guid SerieRegistroId, NumeroGenerado Numero);

/// <summary>
/// Alta de borradores con series (spec no-series, ruling F10): el único punto que resuelve las series de un borrador nuevo y le da
/// número. Lo usan las altas de borradores de factura y de nota y la copia de factura a borrador.
/// </summary>
public static class NumeracionBorrador
{
    /// <summary>
    /// Resuelve y valida las series de un borrador nuevo y numera el borrador, dentro de la transacción del alta:
    /// <list type="number">
    /// <item>Serie de borradores: <paramref name="serieBorradorId"/> o, si es <c>null</c>, la configurada para <paramref name="tipoBorrador"/>.</item>
    /// <item>Serie de registro: <paramref name="serieRegistroId"/> o, si es <c>null</c>, la configurada para <paramref name="tipoRegistro"/>.</item>
    /// <item>La de registro se valida (existe, es de <paramref name="tipoRegistro"/> y está activa) ANTES de numerar: un fallo no consume número.</item>
    /// <item>La de borradores numera con <see cref="IGeneradorNumeroDocumento.SiguienteAsync"/> (que valida tipo y actividad) a <paramref name="fecha"/>.</item>
    /// </list>
    /// Errores: sin configuración, <c>numeracion.sin_configuracion</c> en <c>TipoDocumento</c> (tal cual el motor); los errores del motor
    /// sobre una serie (campo <c>SerieId</c>) se devuelven en <c>SerieRegistroId</c> o <c>SerieBorradorId</c>; los demás
    /// (p. ej. <c>numeracion.sin_transaccion</c>) sin cambios. Exige transacción activa (la del alta).
    /// </summary>
    public static async Task<Result<SeriesBorradorNumeradas>> NumerarBorradorAsync(
        this IGeneradorNumeroDocumento generador,
        TipoDocumentoSerie tipoBorrador,
        TipoDocumentoSerie tipoRegistro,
        Guid? serieBorradorId,
        Guid? serieRegistroId,
        DateOnly fecha,
        CancellationToken cancellationToken = default)
    {
        if (serieBorradorId is null)
        {
            var configurada = await generador.SerieConfiguradaAsync(tipoBorrador, cancellationToken);
            if (!configurada.TryObtenerValor(out var id))
            {
                return Result<SeriesBorradorNumeradas>.Fallo(configurada);
            }

            serieBorradorId = id;
        }

        if (serieRegistroId is null)
        {
            var configurada = await generador.SerieConfiguradaAsync(tipoRegistro, cancellationToken);
            if (!configurada.TryObtenerValor(out var id))
            {
                return Result<SeriesBorradorNumeradas>.Fallo(configurada);
            }

            serieRegistroId = id;
        }

        var registroValida = await generador.ValidarSerieAsync(serieRegistroId.Value, tipoRegistro, cancellationToken);
        if (registroValida.EsFallo)
        {
            return Result<SeriesBorradorNumeradas>.Fallo(EnCampo(registroValida, "SerieRegistroId"));
        }

        var numero = await generador.SiguienteAsync(serieBorradorId.Value, tipoBorrador, fecha, cancellationToken);
        return numero.TryObtenerValor(out var generado)
            ? Result<SeriesBorradorNumeradas>.Exito(new SeriesBorradorNumeradas(serieBorradorId.Value, serieRegistroId.Value, generado))
            : Result<SeriesBorradorNumeradas>.Fallo(EnCampo(numero, "SerieBorradorId"));
    }

    private static Error[] EnCampo(Result fallo, string campo) =>
        [.. fallo.Errores.Select(e => e.Campo == "SerieId" ? e with { Campo = campo } : e)];
}
