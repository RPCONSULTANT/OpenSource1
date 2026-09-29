namespace OpenSource1.Application.Features.GruposClienteContable.Dtos;

/// <summary>
/// DTO de lectura de un grupo contable de cliente, con el número y nombre de cada cuenta (vacíos si la cuenta ya no existe)
/// para rotularlas sin otra consulta. <see cref="Xmin"/> es el token de concurrencia optimista del PUT.
/// </summary>
public sealed class GrupoClienteContableResponse
{
    public Guid Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;
    public Guid CuentaCxCId { get; init; }
    public string CuentaCxCNumero { get; init; } = string.Empty;
    public string CuentaCxCNombre { get; init; } = string.Empty;
    public Guid? CuentaDescuentoId { get; init; }
    public string? CuentaDescuentoNumero { get; init; }
    public string? CuentaDescuentoNombre { get; init; }
    public Guid? CuentaInteresId { get; init; }
    public string? CuentaInteresNumero { get; init; }
    public string? CuentaInteresNombre { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
