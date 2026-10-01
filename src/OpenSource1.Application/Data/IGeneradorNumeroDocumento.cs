using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Data;

/// <summary>
/// Motor de numeración (spec no-series). Emite el siguiente número de una serie, sin huecos ni duplicados bajo concurrencia, dentro
/// de la transacción del documento: <see cref="SiguienteAsync(Guid, TipoDocumentoSerie, DateOnly, CancellationToken)"/> lee la serie
/// <c>FOR SHARE</c> (una desactivación o un cambio de tipo concurrente espera al commit), valida tipo y actividad, bloquea la línea
/// vigente (la última con <c>FechaInicial &lt;= fecha</c>, no bloqueada) <c>FOR UPDATE</c> y actualiza su contador. Si el documento
/// falla, el rollback devuelve el número. Exige transacción activa (<c>numeracion.sin_transaccion</c>).
/// <para>
/// <see cref="ProximoNumeroAsync"/> es solo una vista previa orientativa: no bloquea, no exige transacción y no reserva el número.
/// Con <c>tipoEsperado</c> valida además el tipo de la serie (<c>numeracion.tipo_incorrecto</c>); sin él solo existencia y actividad.
/// <see cref="SiguientePorTipoAsync"/> relee la configuración una vez si la serie configurada dejó de valer mientras esperaba su bloqueo.
/// <see cref="SerieConfiguradaAsync"/> lee la serie predeterminada de un tipo (<c>ConfiguracionesNumeracion</c>).
/// </para>
/// </summary>
public interface IGeneradorNumeroDocumento
{
    Task<Result<NumeroGenerado>> SiguienteAsync(
        Guid serieId, TipoDocumentoSerie tipoEsperado, DateOnly fecha, CancellationToken cancellationToken = default);

    Task<Result<NumeroGenerado>> SiguientePorTipoAsync(TipoDocumentoSerie tipo, DateOnly fecha, CancellationToken cancellationToken = default);

    Task<Result<NumeroGenerado>> ProximoNumeroAsync(
        Guid serieId, DateOnly fecha, TipoDocumentoSerie? tipoEsperado = null, CancellationToken cancellationToken = default);

    Task<Result<Guid>> SerieConfiguradaAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default);

    Task<Result> ValidarSerieAsync(Guid serieId, TipoDocumentoSerie tipoEsperado, CancellationToken cancellationToken = default);
}
