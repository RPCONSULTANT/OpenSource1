using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;

/// <summary>Serie predeterminada de un tipo de documento (una fila por <see cref="TipoDocumentoSerie"/>), con su <c>xmin</c>.</summary>
public sealed class ConfiguracionNumeracionResponse
{
    public TipoDocumentoSerie TipoDocumento { get; init; }
    public string TipoNombre => TipoDocumentoSerieNombres.Nombre(TipoDocumento);
    public Guid SerieId { get; init; }
    public string SerieCodigo { get; init; } = string.Empty;
    public string SerieDescripcion { get; init; } = string.Empty;
    public bool SerieActiva { get; init; }
    public long Xmin { get; init; }
}
