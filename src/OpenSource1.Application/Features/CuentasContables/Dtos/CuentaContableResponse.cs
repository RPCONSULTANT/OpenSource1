using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables.Dtos;

/// <summary>
/// DTO de lectura para CuentaContable. Usa propiedades init (no record posicional) para que Dapper
/// pueda poblar la instancia sin requerir coincidencia exacta del constructor con los tipos del
/// DataReader de Npgsql. <see cref="Xmin"/> es el token de concurrencia optimista (Postgres
/// <c>xmin</c>) que el cliente debe reenviar en el PUT.
/// </summary>
public sealed class CuentaContableResponse
{
    public Guid Id { get; init; }
    public string Numero { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public TipoCuentaContable TipoCuenta { get; init; }
    public TipoResultadoCuenta TipoResultado { get; init; }
    public bool PosteoDirecto { get; init; }
    public bool Bloqueada { get; init; }
    public int Sangria { get; init; }
    public long Xmin { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string? UpdatedBy { get; init; }
}
