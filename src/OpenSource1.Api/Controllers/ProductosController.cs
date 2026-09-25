using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Api.Infrastructure;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Application.Features.Productos.Queries;
using OpenSource1.Application.Security;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Api.Controllers;

[ApiController]
[Route("api/productos")]
public sealed class ProductosController(ISender sender) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<PagedResult<ProductoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string? codigo,
        [FromQuery] string? nombre,
        [FromQuery] string? categoriaCodigo,
        [FromQuery] string? categoriaNombre,
        [FromQuery] string? unidadMedidaCodigo,
        [FromQuery] string? unidadMedidaNombre,
        [FromQuery] string? precioVenta,
        [FromQuery] string? existencia,
        [FromQuery] string? stockState,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = PageRequest.TamanoPorDefecto,
        [FromQuery] string? ordenarPor = null,
        [FromQuery] bool descendente = true,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListProductosQuery(
                new ProductoSearchCriteria(codigo, nombre, categoriaCodigo, categoriaNombre, unidadMedidaCodigo, unidadMedidaNombre, precioVenta, existencia, stockState),
                new PageRequest(pagina, tamanoPagina, ordenarPor, descendente)),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductoResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new GetProductoByIdQuery(id), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Existencia por almacén (Task 3.6): solo los almacenes con movimientos del producto, ordenados por código.
    /// La existencia total (todos los almacenes) va en <c>ProductoResponse.Existencia</c> del listado/detalle.
    /// </summary>
    [HttpGet("{id:guid}/existencias")]
    [Authorize(Policy = ApplicationPolicies.CanConsult)]
    [ProducesResponseType<IReadOnlyList<ExistenciaAlmacen>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExistencias(Guid id, CancellationToken cancellationToken)
    {
        var item = await sender.Send(new GetProductoExistenciasQuery(id), cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>
    /// Alta de producto. <c>categoriaId</c> y <c>unidadMedidaBaseId</c> son opcionales (por defecto la categoría GENERAL y la unidad
    /// UND). <c>costoUnitario</c> y <c>costoAjustado</c> los mantiene el sistema: no forman parte del cuerpo y, si un cliente los
    /// envía, se ignoran (el producto nace con costoUnitario = 0 y costoAjustado = true). <c>stock</c> ya no existe (Task 3.6): si
    /// un cliente lo envía, System.Text.Json lo ignora en silencio (propiedad desconocida); la existencia se registra con diarios
    /// de inventario (Fase 4), no con este alta.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = ApplicationPolicies.CanAdd)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateProductoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new CreateProductoCommand(
                request.Codigo, request.Nombre, request.PrecioVenta, request.CategoriaId, request.UnidadMedidaBaseId,
                request.MetodoCosteo, request.CostoEstandar, request.Bloqueado, request.ImagePath),
            cancellationToken);

        return result.EsFallo
            ? result.ToActionResult()
            : CreatedAtAction(nameof(GetById), new { id = result.Valor.Id }, result.Valor);
    }

    /// <summary>
    /// Modificación de producto con semántica parcial (ver <see cref="UpdateProductoRequest"/>). <c>stock</c> ya no existe
    /// (Task 3.6): se ignora si el cliente lo envía.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanModify)]
    [ProducesResponseType<ProductoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateProductoRequest request, CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new UpdateProductoCommand(
                id, request.Codigo, request.Nombre, request.PrecioVenta, request.CategoriaId, request.UnidadMedidaBaseId,
                request.MetodoCosteo, request.CostoEstandar, request.Bloqueado, request.ImagePath),
            cancellationToken);

        return result.EsFallo ? result.ToActionResult() : Ok(result.Valor);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = ApplicationPolicies.CanDelete)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await sender.Send(new DeleteProductoCommand(id), cancellationToken);
        return deleted ? NoContent() : NotFound();
    }
}

// Alta: los valores por defecto (categoría GENERAL, unidad UND, método Promedio, costo estándar 0, sin bloqueo) permiten omitir los
// campos nuevos. Un valor numérico fuera de un enum no se descarta aquí: lo rechaza el validador (400).
public sealed record CreateProductoRequest(
    string Codigo,
    string Nombre,
    decimal PrecioVenta = 0m,
    Guid? CategoriaId = null,
    Guid? UnidadMedidaBaseId = null,
    MetodoCosteo MetodoCosteo = MetodoCosteo.Promedio,
    decimal CostoEstandar = 0m,
    BloqueoProducto Bloqueado = BloqueoProducto.Ninguno,
    string? ImagePath = null);

/// <summary>
/// Cuerpo de modificación con semántica de modificación parcial: <c>precioVenta</c>, <c>categoriaId</c>,
/// <c>unidadMedidaBaseId</c>, <c>metodoCosteo</c>, <c>costoEstandar</c> y <c>bloqueado</c> son NULABLES: ausente/<c>null</c> = conservar
/// el valor guardado; informado = se aplica (se valida el resultado). Así, un PUT que solo renombra no desbloquea ni cambia el precio,
/// la categoría, la unidad ni los costos del producto. <c>codigo</c>, <c>nombre</c> e <c>imagePath</c> son de reemplazo completo.
/// <c>costoUnitario</c> y <c>costoAjustado</c> no se aceptan (los mantiene el sistema); <c>stock</c> tampoco (ya no existe).
/// </summary>
public sealed record UpdateProductoRequest(
    string Codigo,
    string Nombre,
    decimal? PrecioVenta = null,
    Guid? CategoriaId = null,
    Guid? UnidadMedidaBaseId = null,
    MetodoCosteo? MetodoCosteo = null,
    decimal? CostoEstandar = null,
    BloqueoProducto? Bloqueado = null,
    string? ImagePath = null);
