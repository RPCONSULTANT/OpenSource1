using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables;

public sealed record CuentaContableSearchCriteria(
    string? Numero,
    string? Nombre,
    TipoCuentaContable? TipoCuenta,
    TipoResultadoCuenta? TipoResultado,
    bool? Bloqueada);
