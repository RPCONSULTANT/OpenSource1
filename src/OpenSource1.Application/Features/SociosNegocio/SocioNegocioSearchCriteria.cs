using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SociosNegocio;

/// <summary>
/// Criterios del listado. <c>Codigo</c> y <c>NumeroDocumentoFiscal</c> usan la misma sintaxis de texto
/// que el resto (<c>*</c>, <c>||</c>, <c>&amp;&amp;</c>, sin distinguir mayúsculas); <c>Tipo</c> es una
/// igualdad exacta sobre el <c>smallint</c> del enum (un valor fuera del enum es un 400, no una
/// lista vacía).
/// </summary>
public sealed record SocioNegocioSearchCriteria(
    string? NombreComercial,
    string? Email,
    string? Telefono,
    string? DireccionLinea1,
    string? Sector,
    string? PaisNombre,
    string? Codigo = null,
    TipoSocioNegocio? Tipo = null,
    string? NumeroDocumentoFiscal = null);
