namespace OpenSource1.Application.Features.Entradas.Dtos;

public sealed record EntradaResponse(
    Guid Id,
    string Titulo,
    string? Descripcion,
    string Tipo,
    string Estado,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
