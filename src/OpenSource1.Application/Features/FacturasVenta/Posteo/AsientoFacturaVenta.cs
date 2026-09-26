using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.FacturasVenta.Posteo;

/// <summary>
/// Asiento de una factura de venta (spec 6.5 paso 7 con las desviaciones de la Fase 6), en memoria y sin BD:
/// <list type="bullet">
/// <item>Débito CxC (del grupo de cliente congelado del facturar-a) por <c>ImporteTotal</c>.</item>
/// <item>Crédito Ventas por (GrupoNegocio × GrupoProducto): <c>ROUND(Σ ImporteLinea, 2)</c> por grupo de producto (el grupo de
/// negocio es el de la cabecera, único en el documento); descuento neto, sin pata de descuento.</item>
/// <item>Crédito de cada cuenta de las líneas CuentaContable: <c>ROUND(Σ ImporteLinea, 2)</c> por cuenta.</item>
/// <item>Ajuste de redondeo: la diferencia entre <c>ImporteSinIva</c> (bases agrupadas por IVA) y la suma de los créditos
/// anteriores se suma a la pata de Ventas de MAYOR importe (si no hay líneas de Producto, a la pata de cuenta de mayor importe),
/// para que el asiento cuadre sin cuenta de redondeo.</item>
/// <item>Crédito IVA por grupo de IVA (identificador) con importe distinto de 0.</item>
/// </list>
/// Las patas de importe 0 no se escriben (el libro contable no las admite). Sin patas de costo: las contabiliza el batch de la
/// Fase 5.
/// </summary>
internal static class AsientoFacturaVenta
{
    public static AsientoContable Construir(
        FacturaVentaBorrador borrador,
        IReadOnlyList<LineaFacturaAPostear> lineas,
        TotalesFactura totales,
        CuentasFactura cuentas,
        string numero,
        string descripcion)
    {
        // Patas de ingreso, en orden estable: Ventas por grupo de producto (orden de primera aparición) y luego cuentas.
        var ingresos = new List<PataIngreso>();
        foreach (var grupo in lineas.Where(l => l.Tipo == TipoLineaFactura.Producto).GroupBy(l => l.GrupoProductoId!.Value))
        {
            ingresos.Add(new PataIngreso(
                cuentas.VentasPorGrupoProducto[grupo.Key], Redondear(grupo.Sum(l => l.ImporteLinea)), EsVentas: true, grupo.Key));
        }

        foreach (var grupo in lineas.Where(l => l.Tipo == TipoLineaFactura.CuentaContable).GroupBy(l => l.CuentaContableId!.Value))
        {
            ingresos.Add(new PataIngreso(grupo.Key, Redondear(grupo.Sum(l => l.ImporteLinea)), EsVentas: false, null));
        }

        var diferencia = totales.ImporteSinIva - ingresos.Sum(p => p.Importe);
        if (diferencia != 0m)
        {
            var candidatas = ingresos.Any(p => p.EsVentas) ? ingresos.Where(p => p.EsVentas) : ingresos;
            var mayor = candidatas.OrderByDescending(p => p.Importe).First();
            ingresos[ingresos.IndexOf(mayor)] = mayor with { Importe = mayor.Importe + diferencia };
        }

        var patas = new List<LineaAsiento>
        {
            new(cuentas.CuentaCxCId, totales.ImporteTotal, descripcion, SocioNegocioId: borrador.SocioNegocioFacturarAId),
        };

        patas.AddRange(ingresos
            .Where(p => p.Importe != 0m)
            .Select(p => new LineaAsiento(
                p.CuentaId, -p.Importe, descripcion,
                SocioNegocioId: borrador.SocioNegocioId,
                GrupoNegocioId: p.EsVentas ? borrador.GrupoNegocioId : null,
                GrupoProductoId: p.GrupoProductoId)));

        foreach (var grupo in totales.Grupos.Where(g => g.ImporteIva != 0m))
        {
            var gruposIvaProducto = lineas
                .Where(l => string.Equals(l.IdentificadorIva, grupo.IdentificadorIva, StringComparison.Ordinal))
                .Select(l => l.GrupoIvaProductoId)
                .Distinct()
                .ToList();
            patas.Add(new LineaAsiento(
                cuentas.CuentaIvaPorIdentificador[grupo.IdentificadorIva], -grupo.ImporteIva, descripcion,
                SocioNegocioId: borrador.SocioNegocioId,
                GrupoIvaNegocioId: borrador.GrupoIvaNegocioId,
                GrupoIvaProductoId: gruposIvaProducto.Count == 1 ? gruposIvaProducto[0] : null));
        }

        return new AsientoContable(
            borrador.FechaRegistro, borrador.FechaDocumento, TipoDocumentoContable.FacturaVenta, numero, descripcion,
            TipoOrigenMovimiento.FacturaVenta, numero, patas);
    }

    private static decimal Redondear(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);

    private sealed record PataIngreso(Guid CuentaId, decimal Importe, bool EsVentas, Guid? GrupoProductoId);
}
