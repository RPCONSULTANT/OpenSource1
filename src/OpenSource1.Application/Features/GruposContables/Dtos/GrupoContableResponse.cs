using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.GruposContables.Dtos;

/// <summary>
/// DTO de lectura de un grupo contable simple (cualquiera de los cinco tipos). Propiedades <c>init</c> para que Dapper lo
/// pueble. <see cref="Xmin"/> es el token de concurrencia optimista que el cliente reenvía en el PUT.
/// </summary>
public sealed class GrupoContableResponse
{
    public Guid Id { get; init; }
    public TipoGrupoContable Tipo { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
