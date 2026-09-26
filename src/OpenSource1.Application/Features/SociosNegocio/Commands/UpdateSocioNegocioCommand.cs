using MediatR;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio.Commands;

/// <summary>
/// Modificación de un socio de negocio. Los campos añadidos en la Task 2.6 (<c>RazonSocial</c>,
/// <c>Tipo</c>, <c>TipoDocumentoFiscal</c>, <c>NumeroDocumentoFiscal</c>, <c>Ciudad</c>,
/// <c>TerminoPagoId</c>, <c>LimiteCredito</c>, <c>Bloqueado</c>) tienen semántica de MODIFICACIÓN
/// PARCIAL para que un PUT que no los menciona no los pise (p. ej. desbloquear en silencio un
/// socio bloqueado):
/// <list type="bullet">
/// <item><c>null</c> (ausente) = conservar el valor actual.</item>
/// <item>Valor informado = se aplica.</item>
/// <item>Para limpiar un campo opcional: cadena vacía (<c>RazonSocial</c>, <c>NumeroDocumentoFiscal</c>,
/// <c>Ciudad</c>) o <see cref="Guid.Empty"/> (<c>TerminoPagoId</c>).</item>
/// </list>
/// Los demás campos (<c>NombreComercial</c>, <c>Email</c>, <c>Telefono</c>, dirección, <c>Sector</c>,
/// <c>PaisCodigo</c>, <c>ImagePath</c>) siguen siendo de reemplazo completo, como antes de la 2.6.
/// <c>Codigo</c> es inmutable y no forma parte del comando.
/// Los grupos contables de la Task 5.3 (<c>GrupoNegocioId</c>, <c>GrupoIvaNegocioId</c>, <c>GrupoClienteContableId</c>) son
/// también "null = conservar"; un valor que CAMBIA debe existir (400 <c>socio_negocio.grupo_invalido</c>). A diferencia de
/// <c>TerminoPagoId</c>, <see cref="Guid.Empty"/> NO limpia un grupo (se rechaza como grupo inexistente): solo se sustituye.
/// </summary>
public sealed record UpdateSocioNegocioCommand(
    Guid Id,
    string NombreComercial,
    string? RazonSocial,
    TipoSocioNegocio? Tipo,
    TipoDocumentoFiscal? TipoDocumentoFiscal,
    string? NumeroDocumentoFiscal,
    string? Email,
    string? Telefono,
    string? DireccionLinea1,
    string? DireccionLinea2,
    string? Ciudad,
    string? Sector,
    string? PaisCodigo,
    Guid? TerminoPagoId,
    decimal? LimiteCredito,
    BloqueoSocioNegocio? Bloqueado,
    string? ImagePath = null,
    Guid? GrupoNegocioId = null,
    Guid? GrupoIvaNegocioId = null,
    Guid? GrupoClienteContableId = null) : IRequest<Result<SocioNegocioResponse>>;
