using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.SetupsContables;

/// <summary>
/// Catálogo de los tres setups contables: segmento de ruta de <c>api/setups-contables/{ruta}</c> (y del <c>?tipo=</c> de la
/// página), rótulos y nombres de los parámetros de filtro de cada eje en el listado de la API.
/// </summary>
public static class TiposSetupContable
{
    public sealed record Descriptor(
        TipoSetupContable Tipo, string Ruta, string Nombre, string NombrePlural,
        string ParametroSecundario, string RotuloSecundario, string ParametroPrincipal, string RotuloPrincipal);

    public static readonly IReadOnlyList<Descriptor> Todos =
    [
        new(TipoSetupContable.General, "general", "Setup contable general", "Setup general",
            "grupoNegocioId", "Grupo de negocio", "grupoProductoId", "Grupo de producto"),
        new(TipoSetupContable.Iva, "iva", "Setup de IVA", "Setup de IVA",
            "grupoIvaNegocioId", "Grupo de IVA de negocio", "grupoIvaProductoId", "Grupo de IVA de producto"),
        new(TipoSetupContable.Inventario, "inventario", "Setup de inventario", "Setup de inventario",
            "almacenId", "Almacén", "grupoInventarioId", "Grupo de inventario"),
    ];

    /// <summary>Tipo a partir del segmento de ruta (sin distinguir mayúsculas); <see langword="null"/> si no existe.</summary>
    public static TipoSetupContable? DesdeRuta(string? ruta) =>
        Todos.FirstOrDefault(d => string.Equals(d.Ruta, ruta?.Trim(), StringComparison.OrdinalIgnoreCase))?.Tipo;

    public static Descriptor De(TipoSetupContable tipo) =>
        Todos.FirstOrDefault(d => d.Tipo == tipo) ?? throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de setup contable desconocido.");
}
