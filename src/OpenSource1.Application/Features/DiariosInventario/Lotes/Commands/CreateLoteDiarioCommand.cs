using MediatR;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;

/// <summary>
/// Alta de un lote de diario. <c>PlantillaDiarioId</c> es inmutable tras el alta (no forma parte de
/// <see cref="UpdateLoteDiarioCommand"/>): cambiarla movería el lote a otra familia de tipos de
/// movimiento permitidos, invalidando en silencio las líneas ya capturadas.
/// </summary>
public sealed record CreateLoteDiarioCommand(
    Guid PlantillaDiarioId,
    string Codigo,
    string Nombre,
    Guid? SerieId,
    bool Bloqueado) : IRequest<Result<LoteDiarioResponse>>;
