using OpenSource1.Application.Security;
using OpenSource1.Blazor.Navigation;

namespace OpenSource1.Blazor.Components;

/// <summary>Acciones contextuales de un cliente (Fix-Features C1): las mismas en la lista, la tarjeta y la ficha.</summary>
public static class AccionesCliente
{
    /// <summary>Crear ▾: documentos nuevos con el cliente precargado; llevan returnUrl para que Cancelar vuelva al origen.</summary>
    public static IReadOnlyList<AccionPagina> Crear { get; } =
    [
        new("Factura", IconosModulo.Documento, id => $"/facturas-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true, LlevaRetorno: true),
        new("Nota de crédito", IconosModulo.Documento, id => $"/notas-credito-venta/nueva?socioId={id}", ApplicationPolicies.CanAdd, RequiereSeleccion: true, LlevaRetorno: true),
        // Registrar pagos exige CanModify en la API (CobrosController).
        new("Cobro", IconosModulo.Tarjeta, id => $"/cobros/nuevo?socioId={id}", ApplicationPolicies.CanModify, RequiereSeleccion: true, LlevaRetorno: true),
    ];

    /// <summary>Ver ▾: consultas del cliente. La primera ("Ficha") se omite donde ya es la página o una acción rápida.</summary>
    public static IReadOnlyList<AccionPagina> Ver { get; } =
    [
        new("Ficha", IconosModulo.Persona, id => $"/clientes/{id}", null, RequiereSeleccion: true),
        new("Movimientos de cliente", IconosModulo.Lista, id => $"/ventas/movimientos-cliente?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Estado de cuenta", IconosModulo.Documento, id => $"/ventas/estado-cuenta?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Cobros", IconosModulo.Tarjeta, id => $"/cobros?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
        new("Facturas del cliente", IconosModulo.Documento, id => $"/facturas-venta?socioId={id}", ApplicationPolicies.CanConsult, RequiereSeleccion: true),
    ];

    /// <summary>Ver ▾ sin "Ficha" (para la propia ficha y para las tarjetas, donde "Ficha" ya es acción rápida).</summary>
    public static IReadOnlyList<AccionPagina> VerSinFicha { get; } = [.. Ver.Skip(1)];
}
