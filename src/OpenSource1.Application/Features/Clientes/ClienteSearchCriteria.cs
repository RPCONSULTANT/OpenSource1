namespace OpenSource1.Application.Features.Clientes;

public sealed record ClienteSearchCriteria(
    string? Nombre,
    string? Apellido,
    string? Email,
    string? Telefono,
    string? Direccion);
