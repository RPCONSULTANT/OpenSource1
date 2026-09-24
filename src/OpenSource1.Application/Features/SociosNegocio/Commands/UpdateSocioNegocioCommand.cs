using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

public sealed record UpdateSocioNegocioCommand(
    Guid Id,
    string NombreComercial,
    string? RazonSocial,
    TipoSocioNegocio Tipo,
    TipoDocumentoFiscal TipoDocumentoFiscal,
    string? NumeroDocumentoFiscal,
    string? Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? Sector,
    string? PaisCodigo,
    Guid? TerminoPagoId,
    decimal LimiteCredito,
    BloqueoSocioNegocio Bloqueado,
    string? ImagePath = null) : IRequest<Result<SocioNegocioResponse>>, IDatosSocioNegocio;
