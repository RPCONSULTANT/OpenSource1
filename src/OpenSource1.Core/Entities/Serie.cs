using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities;

/// <summary>
/// Serie de numeración (spec no-series). Agrupa una o más <see cref="LineaSerie"/> vigentes por fecha. <see cref="TipoDocumento"/>
/// decide qué documento numera (el generador lo valida); una serie inactiva no numera ni se asigna. <see cref="PermiteHuecos"/> es
/// documental: el generador bloquea la línea igual en ambos casos (D9).
/// </summary>
public sealed class Serie : BaseEntity
{
    public required string Codigo { get; set; }
    public required string Descripcion { get; set; }
    public TipoDocumentoSerie TipoDocumento { get; set; }
    public bool PermiteHuecos { get; set; }
    public bool Activa { get; set; } = true;
}
