namespace OpenSource1.Application.Features.SociosNegocio;

public sealed record SocioNegocioSearchCriteria(
    string? NombreComercial,
    string? Email,
    string? Telefono,
    string? DireccionLinea1,
    string? Sector,
    string? PaisNombre);
