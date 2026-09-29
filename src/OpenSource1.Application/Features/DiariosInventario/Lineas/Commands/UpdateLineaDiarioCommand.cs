using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lineas.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas.Commands;

/// <summary>
/// Modificación de una línea de diario. Reemplazo completo (no hay semántica "null = conservar": un
/// borrador de un solo paso siempre reenvía todos sus campos). <c>LoteDiarioId</c> y <c>NumeroLinea</c>
/// son inmutables y no forman parte del comando. <c>Xmin</c> es obligatorio (concurrencia optimista):
/// si no coincide con el vigente, 409 <c>entidad.modificada_por_otro</c>.
/// </summary>
public sealed record UpdateLineaDiarioCommand(
    Guid Id,
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
    string? Descripcion,
    long Xmin) : IRequest<Result<LineaDiarioResponse>>;
