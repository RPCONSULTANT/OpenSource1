using OpenSource1.Blazor.Services;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>Una opción de un <c>&lt;select&gt;</c> de grupo contable.</summary>
public sealed record GrupoOpcion(Guid Id, string Codigo, string Descripcion)
{
    public string Rotulo => $"{Codigo} — {Descripcion}";
}

/// <summary>
/// Grupos contables elegibles para la ficha de producto (Task 5.3) y si su carga fue fiable. A diferencia de categorías y
/// unidades (<see cref="ProductoOpciones"/>), un fallo aquí NO bloquea guardar la ficha: solo impide CAMBIAR los grupos (los
/// selectores se muestran deshabilitados con el valor vigente y la página envía null = conservar).
/// </summary>
public sealed record GruposProductoOpciones(
    IReadOnlyList<GrupoOpcion> Producto, IReadOnlyList<GrupoOpcion> IvaProducto, IReadOnlyList<GrupoOpcion> Inventario, bool CargaFallida)
{
    public static readonly GruposProductoOpciones SinCargar = new([], [], [], false);

    public const string MensajeNoDisponibles =
        "No fue posible cargar los grupos contables: puede guardar el resto de la ficha, pero los grupos contables no se pueden cambiar hasta que carguen (se conservan los actuales).";

    public const string MensajeNoDisponiblesAlta =
        "No fue posible cargar los grupos contables: puede guardar el producto y se le asignarán los grupos por defecto (BIENES, ITBIS18, GENERAL); podrá cambiarlos después.";

    public static async Task<GruposProductoOpciones> CargarAsync(IGrupoContableApiClient client, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var producto = client.ListAllAsync(TipoGrupoContable.Producto, cts.Token);
            var ivaProducto = client.ListAllAsync(TipoGrupoContable.IvaProducto, cts.Token);
            var inventario = client.ListAllAsync(TipoGrupoContable.Inventario, cts.Token);
            await Task.WhenAll(producto, ivaProducto, inventario);
            return new GruposProductoOpciones(
                GruposOpciones.Mapear(await producto), GruposOpciones.Mapear(await ivaProducto), GruposOpciones.Mapear(await inventario), false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load grupos contables de producto from API.");
            return new GruposProductoOpciones([], [], [], true);
        }
    }
}

/// <summary>Grupos contables elegibles para la ficha de socio/cliente (Task 5.3); mismo criterio que <see cref="GruposProductoOpciones"/>.</summary>
public sealed record GruposSocioOpciones(
    IReadOnlyList<GrupoOpcion> Negocio, IReadOnlyList<GrupoOpcion> IvaNegocio, IReadOnlyList<GrupoOpcion> ClienteContable, bool CargaFallida)
{
    public static readonly GruposSocioOpciones SinCargar = new([], [], [], false);

    public const string MensajeNoDisponibles =
        "No fue posible cargar los grupos contables: puede guardar el resto de la ficha, pero los grupos contables no se pueden cambiar hasta que carguen (se conservan los actuales).";

    public const string MensajeNoDisponiblesAlta =
        "No fue posible cargar los grupos contables: puede guardar el cliente y se le asignarán los grupos por defecto (NACIONAL, ITBIS18, GENERAL); podrá cambiarlos después.";

    public static async Task<GruposSocioOpciones> CargarAsync(
        IGrupoContableApiClient grupos, IGrupoClienteContableApiClient gruposCliente, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var negocio = grupos.ListAllAsync(TipoGrupoContable.Negocio, cts.Token);
            var ivaNegocio = grupos.ListAllAsync(TipoGrupoContable.IvaNegocio, cts.Token);
            var cliente = gruposCliente.ListAllAsync(cts.Token);
            await Task.WhenAll(negocio, ivaNegocio, cliente);
            return new GruposSocioOpciones(
                GruposOpciones.Mapear(await negocio),
                GruposOpciones.Mapear(await ivaNegocio),
                [.. (await cliente).Select(g => new GrupoOpcion(g.Id, g.Codigo, g.Descripcion))],
                false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load grupos contables de socio from API.");
            return new GruposSocioOpciones([], [], [], true);
        }
    }
}

public static class GruposOpciones
{
    public static IReadOnlyList<GrupoOpcion> Mapear(IReadOnlyList<OpenSource1.Application.Features.GruposContables.Dtos.GrupoContableResponse> grupos) =>
        [.. grupos.Select(g => new GrupoOpcion(g.Id, g.Codigo, g.Descripcion))];

    /// <summary>El Id por defecto si está entre las opciones cargadas (para preseleccionar la semilla en un alta); si no, null.</summary>
    public static Guid? PorDefecto(IReadOnlyList<GrupoOpcion> opciones, Guid id) => opciones.Any(o => o.Id == id) ? id : null;
}
