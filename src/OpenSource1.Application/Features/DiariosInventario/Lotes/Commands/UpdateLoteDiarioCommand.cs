using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;

/// <summary>
/// Modificación de un lote de diario. <c>Codigo</c> y <c>Nombre</c> son de reemplazo completo (mismo
/// patrón que Almacen). <c>SerieId</c> tiene semántica de MODIFICACIÓN PARCIAL: <see langword="null"/>
/// (ausente) = conservar la serie actual; <see cref="Guid.Empty"/> = limpiarla (usar la de la plantilla);
/// un id concreto = usar esa serie. <c>Bloqueado</c> es <see langword="null"/> = conservar.
/// <c>Xmin</c> es obligatorio: es el valor visto por el cliente al leer el lote (concurrencia optimista
/// vía <c>xmin</c> de Postgres); si no coincide con el vigente, la modificación falla con 409
/// <c>entidad.modificada_por_otro</c>.
/// </summary>
public sealed record UpdateLoteDiarioCommand(
    Guid Id,
    string Codigo,
    string Nombre,
    Guid? SerieId,
    bool? Bloqueado,
    long Xmin) : IRequest<Result<LoteDiarioResponse>>;
