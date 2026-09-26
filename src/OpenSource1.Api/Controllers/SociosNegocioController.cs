using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.SociosNegocio;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Dtos;
using OpenSource1.Application.Features.SociosNegocio.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/socios-negocio")]
public sealed class SociosNegocioController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<SocioNegocioResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? nombreComercial,
        [FromQuery] string? email,
        [FromQuery] string? telefono,
        [FromQuery] string? direccion,
        [FromQuery] string? sector,
        [FromQuery] string? pais,
        [FromQuery] string? codigo,
        [FromQuery] TipoSocioNegocio? tipo,
        [FromQuery] string? numeroDocumentoFiscal,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListSociosNegocioQuery(
                new SocioNegocioSearchCriteria(
                    nombreComercial, email, telefono, direccion, sector, pais, codigo, tipo, numeroDocumentoFiscal),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<SocioNegocioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetSocioNegocioByIdQuery(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    /// <summary>
    /// Alta de socio de negocio. El <c>Codigo</c> lo asigna el sistema (serie SOCIOS): el cuerpo
    /// NO tiene ese campo, y si un cliente lo envía se ignora (el deserializador descarta las
    /// propiedades desconocidas).
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<SocioNegocioResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateSocioNegocioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateSocioNegocioCommand(
                request.NombreComercial, request.RazonSocial, request.Tipo, request.TipoDocumentoFiscal,
                request.NumeroDocumentoFiscal, request.Email, request.Telefono, request.DireccionLinea1,
                request.DireccionLinea2, request.Ciudad, request.Sector, request.PaisCodigo, request.TerminoPagoId,
                request.LimiteCredito, request.Bloqueado, request.ImagePath,
                request.GrupoNegocioId, request.GrupoIvaNegocioId, request.GrupoClienteContableId),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    /// <summary>
    /// Modificación de socio de negocio. El <c>Codigo</c> es inmutable: no forma parte del cuerpo.
    /// Modificación parcial de los campos de la 2.6 (ver <see cref="UpdateSocioNegocioRequest"/>).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<SocioNegocioResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateSocioNegocioRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateSocioNegocioCommand(
                id, request.NombreComercial, request.RazonSocial, request.Tipo, request.TipoDocumentoFiscal,
                request.NumeroDocumentoFiscal, request.Email, request.Telefono, request.DireccionLinea1,
                request.DireccionLinea2, request.Ciudad, request.Sector, request.PaisCodigo, request.TerminoPagoId,
                request.LimiteCredito, request.Bloqueado, request.ImagePath,
                request.GrupoNegocioId, request.GrupoIvaNegocioId, request.GrupoClienteContableId),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new DeleteSocioNegocioCommand(id), cancellationToken);
        return result.EsFallo ? result.ToActionResult() : NoContent();
    }
}

// Alta: los valores por defecto (Tipo=Cliente, TipoDocumentoFiscal=SinDocumento, LimiteCredito=0,
// Bloqueado=Ninguno) permiten omitir los campos nuevos. Un valor numérico fuera del enum no se
// descarta aquí: lo rechaza el validador (400).
public sealed record CreateSocioNegocioRequest(
    string NombreComercial,
    string? RazonSocial = null,
    TipoSocioNegocio Tipo = TipoSocioNegocio.Cliente,
    TipoDocumentoFiscal TipoDocumentoFiscal = TipoDocumentoFiscal.SinDocumento,
    string? NumeroDocumentoFiscal = null,
    string? Email = null,
    string? Telefono = null,
    string? DireccionLinea1 = null,
    string? DireccionLinea2 = null,
    string? Ciudad = null,
    string? Sector = null,
    string? PaisCodigo = null,
    Guid? TerminoPagoId = null,
    decimal LimiteCredito = 0m,
    BloqueoSocioNegocio Bloqueado = BloqueoSocioNegocio.Ninguno,
    string? ImagePath = null,
    Guid? GrupoNegocioId = null,
    Guid? GrupoIvaNegocioId = null,
    Guid? GrupoClienteContableId = null);

/// <summary>
/// Cuerpo de modificación. Los campos de la Task 2.6 son NULABLES con semántica de modificación
/// parcial: ausente/<c>null</c> = conservar el valor actual del socio; informado = se aplica. Para
/// limpiar un campo opcional se envía cadena vacía (<c>razonSocial</c>, <c>numeroDocumentoFiscal</c>,
/// <c>ciudad</c>) o el Guid vacío <c>00000000-0000-0000-0000-000000000000</c> (<c>terminoPagoId</c>).
/// Los demás campos son de reemplazo completo. Así, un PUT que solo renombra no desbloquea ni
/// rebaja el límite de un socio. Grupos contables (Task 5.3) <c>grupoNegocioId</c>, <c>grupoIvaNegocioId</c>,
/// <c>grupoClienteContableId</c>: ausente/<c>null</c> = conservar; no se pueden quitar (el Guid vacío se rechaza como grupo
/// inexistente, 400 <c>socio_negocio.grupo_invalido</c>).
/// </summary>
public sealed record UpdateSocioNegocioRequest(
    string NombreComercial,
    string? RazonSocial = null,
    TipoSocioNegocio? Tipo = null,
    TipoDocumentoFiscal? TipoDocumentoFiscal = null,
    string? NumeroDocumentoFiscal = null,
    string? Email = null,
    string? Telefono = null,
    string? DireccionLinea1 = null,
    string? DireccionLinea2 = null,
    string? Ciudad = null,
    string? Sector = null,
    string? PaisCodigo = null,
    Guid? TerminoPagoId = null,
    decimal? LimiteCredito = null,
    BloqueoSocioNegocio? Bloqueado = null,
    string? ImagePath = null,
    Guid? GrupoNegocioId = null,
    Guid? GrupoIvaNegocioId = null,
    Guid? GrupoClienteContableId = null);
