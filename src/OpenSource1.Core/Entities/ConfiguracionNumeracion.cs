using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities;

/// <summary>
/// Serie predeterminada de un tipo de documento (spec no-series, "Enfoque 1"): una fila por <see cref="TipoDocumentoSerie"/>. Los
/// borradores guardan sus series al crearse: cambiar esta tabla no afecta a documentos ya creados.
/// </summary>
public sealed class ConfiguracionNumeracion : BaseEntity
{
    public TipoDocumentoSerie TipoDocumento { get; set; }
    public Guid SerieId { get; set; }
}

public static class ConfiguracionNumeracionIds
{
    public static Guid De(TipoDocumentoSerie tipo) => Guid.Parse($"e2000000-0000-0000-0000-{(short)tipo:D12}");
}
