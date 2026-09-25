using MediatR;
using OpenSource1.Application.Services.Inventario;

namespace OpenSource1.Application.Features.Productos.Queries;

/// <summary>
/// Existencia por almacén de un producto (Task 3.6), delegada en <see cref="IConsultaInventario.ExistenciasPorAlmacenAsync"/>:
/// solo trae los almacenes que tienen movimientos del producto. <see langword="null"/> = el producto no existe (404).
/// </summary>
public sealed record GetProductoExistenciasQuery(Guid ProductoId) : IRequest<IReadOnlyList<ExistenciaAlmacen>?>;
