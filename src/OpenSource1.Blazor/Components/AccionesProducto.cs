using OpenSource1.Application.Security;
using OpenSource1.Blazor.Navigation;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Acciones contextuales de un producto (Fix-Features C2), compartidas por la barra de la lista, las tarjetas y la ficha.
/// Todas reciben el id del producto seleccionado. <see cref="Ver"/> empieza por "Ficha"; la propia ficha usa
/// <see cref="VerDesdeFicha"/> (sin enlazarse a sí misma).
/// </summary>
public static class AccionesProducto
{
    public static IReadOnlyList<AccionPagina> Crear { get; } =
    [
        new("Ajuste en diario de inventario", IconosModulo.Lista, id => $"/diarios-inventario/nuevo?productoId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true),
    ];

    public static IReadOnlyList<AccionPagina> Ver { get; } =
    [
        new("Ficha", IconosModulo.Cubo, id => $"/productos/{id}", null, RequiereSeleccion: true),
        new("Existencias", IconosModulo.Tabla, id => $"/inventario/existencias?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Movimientos de producto", IconosModulo.Lista, id => $"/inventario/movimientos-producto?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Movimientos de valor", IconosModulo.Lista, id => $"/inventario/movimientos-valor?productoId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
    ];

    /// <summary><see cref="Ver"/> sin "Ficha": para la propia ficha y para el menú ⋯ de las tarjetas (que ya muestran "Ficha").</summary>
    public static IReadOnlyList<AccionPagina> VerDesdeFicha { get; } = [.. Ver.Skip(1)];
}
