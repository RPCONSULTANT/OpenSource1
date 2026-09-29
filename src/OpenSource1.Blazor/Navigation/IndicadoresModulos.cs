using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Common;

namespace OpenSource1.Blazor.Navigation;

public sealed class IndicadoresModulos(
    IAuthorizationService autorizacion,
    ISocioNegocioApiClient socios,
    IProductoApiClient productos,
    ICategoriaProductoApiClient categorias,
    IAlmacenApiClient almacenes,
    IFacturaVentaApiClient facturas,
    INotaCreditoVentaApiClient notas,
    ICuentaContableApiClient cuentas,
    ITerminoPagoApiClient terminos,
    IUnidadMedidaApiClient unidades,
    ILogger<IndicadoresModulos> logger) : IIndicadoresModulos
{
    // Solo el total: una página de un elemento basta para leer PagedResult.Total.
    private static readonly PageRequest Uno = new(1, 1);

    public async Task<IReadOnlyList<Indicador>> ObtenerAsync(string grupo, ClaimsPrincipal usuario, CancellationToken cancellationToken = default)
    {
        if (!(await autorizacion.AuthorizeAsync(usuario, ApplicationPolicies.CanConsult)).Succeeded)
        {
            return [];
        }

        var resultado = new List<Indicador>();
        foreach (var (titulo, ruta, contar) in Definiciones(grupo, cancellationToken).Take(3))
        {
            try
            {
                resultado.Add(new Indicador(titulo, (await contar()).ToString("N0", CultureInfo.InvariantCulture), ruta));
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "No fue posible calcular el indicador {Indicador} del grupo {Grupo}.", titulo, grupo);
            }
        }

        return resultado;
    }

    private List<(string Titulo, string? Ruta, Func<Task<long>> Contar)> Definiciones(string grupo, CancellationToken ct) => grupo switch
    {
        "clientes" =>
        [
            ("Clientes registrados", "/clientes", async () => (await socios.ListAsync(null, Uno, ct)).Total),
        ],
        "productos" =>
        [
            ("Productos", "/productos", async () => (await productos.ListAsync(null, Uno, ct)).Total),
            ("Sin existencia", "/productos", async () => (await productos.ListAsync(new ProductoSearchFilter(null, null, null, null, null, null, null, null, "without"), Uno, ct)).Total),
            ("Categorías", "/categorias-producto", async () => (await categorias.ListAsync(null, Uno, ct)).Total),
        ],
        "inventario" =>
        [
            ("Almacenes", "/almacenes", async () => (await almacenes.ListAsync(null, Uno, ct)).Total),
        ],
        "facturacion" =>
        [
            ("Borradores abiertos", "/facturas-venta/borradores?estado=1", async () => (await facturas.ListBorradoresAsync(new FacturaVentaBorradorFiltro(null, null, null, 1), Uno, ct)).Total),
            ("Facturas posteadas", "/facturas-venta", async () => (await facturas.ListFacturasAsync(null, Uno, ct)).Total),
            ("Borradores de nota de crédito", "/notas-credito-venta/borradores", async () => (await notas.ListBorradoresAsync(null, Uno, ct)).Total),
        ],
        "contabilidad" =>
        [
            ("Cuentas contables", "/cuentas-contables", async () => (await cuentas.ListAsync(null, Uno, ct)).Total),
        ],
        "configuracion" =>
        [
            ("Términos de pago", "/terminos-pago", async () => (await terminos.ListAsync(null, Uno, ct)).Total),
            ("Unidades de medida", "/unidades-medida", async () => (await unidades.ListAsync(null, Uno, ct)).Total),
        ],
        _ => [],
    };
}
