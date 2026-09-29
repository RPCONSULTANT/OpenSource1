using System.Globalization;
using OpenSource1.Application.Features.Almacenes.Dtos;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Opciones y etiquetas comunes de las vistas de inventario (Task 7.2): almacenes (TODOS, también los bloqueados: un filtro
/// de consulta debe poder ver la historia de cualquier almacén), productos buscados por nombre (primeros
/// <see cref="MaximoProductos"/>) y las etiquetas de los enums del libro.
/// </summary>
public static class InventarioVistasOpciones
{
    public const int MaximoProductos = 50;

    public static readonly IReadOnlyList<(int Valor, string Etiqueta)> TiposMovimiento =
    [
        ((int)TipoMovimientoInventario.Compra, "Compra"),
        ((int)TipoMovimientoInventario.Venta, "Venta"),
        ((int)TipoMovimientoInventario.AjustePositivo, "Ajuste positivo"),
        ((int)TipoMovimientoInventario.AjusteNegativo, "Ajuste negativo"),
        ((int)TipoMovimientoInventario.Transferencia, "Transferencia"),
    ];

    public static readonly IReadOnlyList<(int Valor, string Etiqueta)> TiposOrigen =
    [
        ((int)TipoOrigenMovimiento.Diario, "Diario"),
        ((int)TipoOrigenMovimiento.FacturaVenta, "Factura de venta"),
        ((int)TipoOrigenMovimiento.AjusteCosto, "Ajuste de costo"),
        ((int)TipoOrigenMovimiento.CostoInventario, "Costo de inventario"),
        ((int)TipoOrigenMovimiento.Cobro, "Cobro"),
        ((int)TipoOrigenMovimiento.NotaCreditoVenta, "Nota de crédito de venta"),
        ((int)TipoOrigenMovimiento.Migracion, "Migración"),
    ];

    public static string TipoMovimiento(TipoMovimientoInventario tipo) =>
        TiposMovimiento.FirstOrDefault(x => x.Valor == (int)tipo).Etiqueta ?? tipo.ToString();

    /// <summary>Enlace a la ficha del documento de origen (factura o nota de crédito de venta); null si no tiene ficha.</summary>
    public static string? UrlDocumento(TipoDocumentoInventario tipo, string? numero) => string.IsNullOrEmpty(numero)
        ? null
        : tipo switch
        {
            TipoDocumentoInventario.FacturaVenta => $"/facturas-venta/{Uri.EscapeDataString(numero)}",
            TipoDocumentoInventario.NotaCreditoVenta => $"/notas-credito-venta/{Uri.EscapeDataString(numero)}",
            _ => null
        };

    public static string TipoOrigen(TipoOrigenMovimiento origen) =>
        TiposOrigen.FirstOrDefault(x => x.Valor == (int)origen).Etiqueta ?? origen.ToString();

    public static string TipoValor(TipoValor tipo) => tipo switch
    {
        Core.Enums.TipoValor.CostoDirecto => "Costo directo",
        Core.Enums.TipoValor.Redondeo => "Redondeo",
        _ => tipo.ToString()
    };

    /// <summary>Cantidades del libro (numeric(18,6)): sin ceros de relleno.</summary>
    public static string Cantidad(decimal valor) => valor.ToString("#,##0.######", CultureInfo.InvariantCulture);

    public static string Cantidad(decimal? valor) => valor is { } v ? Cantidad(v) : "—";

    /// <summary>Costos unitarios (hasta 4 decimales, como <c>CostoPorUnidad</c>; el costo medio se muestra igual).</summary>
    public static string CostoUnitario(decimal valor) => valor.ToString("#,##0.00##", CultureInfo.InvariantCulture);

    public static async Task<OpcionesCargadas<AlmacenResponse>> AlmacenesAsync(
        IAlmacenApiClient client, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var pagina = await client.ListAsync(null, new PageRequest(1, PageRequest.TamanoMaximo, "Codigo", Descendente: false), cts.Token);
            return new(pagina.Items, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load almacenes options from API.");
            return new([], true);
        }
    }

    public static async Task<OpcionesCargadas<ProductoResponse>> ProductosAsync(
        IProductoApiClient client, string? busqueda, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ProductoOpciones.TiempoMaximo);
        try
        {
            var filtro = new ProductoSearchFilter(
                null, string.IsNullOrWhiteSpace(busqueda) ? null : busqueda.Trim(), null, null, null, null, null, null);
            var pagina = await client.ListAsync(filtro, new PageRequest(1, MaximoProductos, "Codigo", Descendente: false), cts.Token);
            return new(pagina.Items, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load productos options from API.");
            return new([], true);
        }
    }

    /// <summary>
    /// Carga el producto elegido si no está entre las opciones (búsqueda distinta, o fuera de los primeros
    /// <see cref="MaximoProductos"/>): el select siempre muestra el valor vigente con su código y nombre.
    /// </summary>
    public static async Task<ProductoResponse?> ProductoVigenteAsync(
        IProductoApiClient client, Guid? productoId, IReadOnlyList<ProductoResponse> opciones, ILogger logger)
    {
        if (productoId is not { } id)
        {
            return null;
        }

        var enLista = opciones.FirstOrDefault(p => p.Id == id);
        if (enLista is not null)
        {
            return enLista;
        }

        try
        {
            return await client.GetByIdAsync(id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load producto {Id}.", id);
            return null;
        }
    }

    /// <summary>Guid de un filtro de la query string; vacío o no interpretable = sin filtro (ver <see cref="FiltroInvalido"/>).</summary>
    public static Guid? ParseGuid(string? texto) => Guid.TryParse(texto, out var valor) ? valor : null;

    /// <summary>Entero de un filtro de la query string; vacío o no interpretable = null.</summary>
    public static int? ParseEntero(string? texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var valor) ? valor : null;

    /// <summary>
    /// Mensaje del primer filtro de la URL con texto que no se puede interpretar (Guid para los ids, entero para los tipos), o null.
    /// Las páginas lo muestran como aviso en lugar de consultar: un id manipulado no debe convertirse en "sin filtro" en silencio.
    /// </summary>
    public static string? FiltroInvalido(params (string Nombre, string? Texto)[] filtros)
    {
        foreach (var (nombre, texto) in filtros)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                continue;
            }

            var valido = nombre.EndsWith("Id", StringComparison.Ordinal) ? ParseGuid(texto) is not null : ParseEntero(texto) is not null;
            if (!valido)
            {
                return $"El filtro «{nombre}» de la dirección no es válido ('{texto}').";
            }
        }

        return null;
    }

    /// <summary>Texto de un filtro de fecha: vacío = sin filtro; inválido = <paramref name="error"/> con el mensaje para el aviso.</summary>
    public static bool TryFecha(string? texto, string etiqueta, out DateOnly? valor, out string? error)
    {
        valor = null;
        error = null;
        if (string.IsNullOrWhiteSpace(texto))
        {
            return true;
        }

        if (EntradaFecha.TryParse(texto, out var fecha, out error, etiqueta))
        {
            valor = fecha;
            return true;
        }

        return false;
    }
}
