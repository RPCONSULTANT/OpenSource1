using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

/// <summary>
/// Alta de socio de negocio. Los grupos contables (Task 5.3) son opcionales: <see langword="null"/> = el socio nace sin ese grupo
/// (la API no aplica grupos por defecto; la UI preselecciona los semilla). Si vienen, deben existir (400
/// <c>socio_negocio.grupo_invalido</c>).
/// </summary>
public sealed record CreateSocioNegocioCommand(
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
    string? ImagePath = null,
    Guid? GrupoNegocioId = null,
    Guid? GrupoIvaNegocioId = null,
    Guid? GrupoClienteContableId = null) : IRequest<Result<SocioNegocioResponse>>, IDatosSocioNegocio;
