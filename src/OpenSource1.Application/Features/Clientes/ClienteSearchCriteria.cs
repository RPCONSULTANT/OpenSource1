namespace OpenSource1.Application.Features.Clientes;

public sealed record ClienteSearchCriteria(
    Guid? Id,
    string? DocumentoIdentidad,
    string? Nombre,
    bool? Activo);
