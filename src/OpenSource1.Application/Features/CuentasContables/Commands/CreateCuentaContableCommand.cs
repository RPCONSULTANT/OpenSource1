using MediatR;
using OpenSource1.Application.Features.CuentasContables.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.CuentasContables.Commands;

public sealed record CreateCuentaContableCommand(
    string Numero,
    string Nombre,
    TipoCuentaContable TipoCuenta,
    TipoResultadoCuenta TipoResultado,
    bool PosteoDirecto,
    bool Bloqueada,
    int Sangria) : IRequest<Result<CuentaContableResponse>>;
