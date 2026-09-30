using OpenSource1.Application.Features.Series.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Series;

/// <summary>Protecciones de una serie: ya emitió números, está en la configuración, o la usan plantillas/lotes de diario.</summary>
public sealed record UsoSerie(bool Usada, bool Asignada, bool Referenciada);

/// <summary>Línea viva de una serie de un tipo, para detectar rangos solapados entre series del mismo tipo.</summary>
public sealed record LineaDeTipo(Guid LineaId, Guid SerieId, string SerieCodigo, string NumeroInicial, string NumeroFinal);

public interface ISerieReadRepository
{
    Task<SerieDetalleResponse?> GetByIdAsync(Guid id, DateOnly hoy, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<SerieResponse>>> ListAsync(
        SerieSearchCriteria search, PageRequest paginacion, DateOnly hoy, CancellationToken cancellationToken = default);

    Task<UsoSerie> UsoAsync(Guid serieId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LineaDeTipo>> LineasDelTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bloquea la fila de la serie <c>FOR UPDATE</c> (exige transacción) y devuelve su tipo, o null si no existe. Toda transacción de
    /// administración lo llama ANTES de leer o escribir sus líneas: mismo orden que el motor (Series antes que LineasSerie, sin
    /// interbloqueos) y un posteo en curso termina antes de que se evalúe si la serie o la línea están usadas.
    /// </summary>
    Task<TipoDocumentoSerie?> BloquearSerieAsync(Guid serieId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializa (bloqueo consultivo de transacción, tras <see cref="BloquearSerieAsync"/>) las altas y cambios de líneas y de tipo
    /// de un mismo tipo de documento: dos administradores no pueden crear a la vez rangos solapados en series distintas.
    /// </summary>
    Task BloquearTipoAsync(TipoDocumentoSerie tipo, CancellationToken cancellationToken = default);
}
