using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;

/// <summary>
/// Alta de una línea de diario. <c>NumeroLinea</c> no forma parte del comando: lo asigna el sistema
/// (máximo vigente del lote + 10000, o 10000 si no hay ninguna).
/// </summary>
public sealed record CreateLineaDiarioCommand(
    Guid LoteDiarioId,
    DateOnly FechaRegistro,
    DateOnly FechaDocumento,
    string? NumeroDocumento,
    TipoMovimientoInventario TipoMovimiento,
    Guid ProductoId,
    Guid AlmacenId,
    Guid? AlmacenDestinoId,
    Guid UnidadMedidaId,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Descripcion) : IRequest<Result<LineaDiarioResponse>>;
