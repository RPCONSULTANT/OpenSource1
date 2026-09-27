using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta.Posteo;

/// <summary>
/// Importes de una nota de crédito RECALCULADOS desde la línea de la factura y TOPADOS por lo que queda por acreditar (Ruling FI):
/// nunca se acredita de más en importe, IVA ni costo, aunque la cantidad esté topada, porque cada nota parcial redondea por su cuenta
/// (p. ej. 3 × 3.33 al 10 % factura 8.99, pero tres notas de 1 darían 3 × 3.00 = 9.00). Puro, sin BD: lo usan el posteo (bajo el
/// bloqueo de la factura, con lo acreditado por notas POSTEADAS) y la vista previa de totales del borrador.
/// <list type="bullet">
/// <item>Línea: <c>ImporteLinea = min(calculado, ImporteLinea de la factura − Σ acreditado)</c> e igual el descuento; la nota que
/// AGOTA la cantidad pendiente de la línea toma EXACTAMENTE los remanentes (Σ de las notas = lo facturado, al céntimo). El importe
/// y el descuento de la nota que agota son cada uno el remanente del de la factura (Σ descuentos = descuento facturado); en esa
/// nota concreta <c>ImporteLinea</c> puede no ser <c>ROUND(Cantidad × Precio, 2) − descuento</c>.</item>
/// <item>IVA: la calculadora agrupa las líneas YA topadas (base = Σ importes topados redondeada) y DESPUÉS se topa el IVA de cada
/// grupo: <c>min(calculado, IVA del grupo en la factura − Σ IVA acreditado del grupo)</c>; si tras la nota ya no queda cantidad
/// pendiente en ninguna línea del grupo, el grupo toma el remanente exacto; si la nota agota TODA la factura, además se añaden los
/// grupos con remanente que la nota no traía (base 0). Las líneas de IVA y el asiento de la nota usan estos importes topados; el
/// ajuste de redondeo de Ventas del asiento (<c>ImporteSinIva</c> − Σ patas) sigue garantizando el cuadre.</item>
/// <item>Costo de la devolución: <c>min(calculado, valor de la salida original − Σ ya devuelto de esa línea)</c>, con el remanente
/// exacto cuando la devolución agota la cantidad facturada de la línea.</item>
/// </list>
/// Los remanentes nunca son negativos (se acotan a 0).
/// </summary>
internal static class TopesNotaCredito
{
    public sealed record Resultado(IReadOnlyList<LineaNotaAPostear> Lineas, TotalesFactura Totales, bool AgotaFactura);

    /// <summary>Copia en la línea de la nota los valores de su línea de factura (lo que el borrador copió, releído del documento).</summary>
    public static LineaNotaAPostear DesdeOriginal(LineaNotaAPostear l, LineaFacturaVenta o) => l with
    {
        NumeroLinea = o.NumeroLinea,
        Tipo = o.Tipo,
        ProductoId = o.ProductoId,
        CuentaContableId = o.CuentaContableId,
        Descripcion = o.Descripcion,
        AlmacenId = o.AlmacenId,
        UnidadMedidaId = o.UnidadMedidaId,
        CantidadPorUnidadMedida = o.CantidadPorUnidadMedida,
        PrecioUnitario = o.PrecioUnitario,
        PorcentajeDescuentoLinea = o.PorcentajeDescuentoLinea,
        GrupoProductoId = o.GrupoProductoId,
        GrupoIvaProductoId = o.GrupoIvaProductoId,
        GrupoInventarioId = o.GrupoInventarioId,
        IdentificadorIva = o.IdentificadorIva,
        PorcentajeIva = o.PorcentajeIva,
    };

    /// <summary>
    /// Importes recalculados y topados de todas las líneas y totales con el IVA topado. Las líneas cuya línea de factura no está en
    /// <paramref name="lineasFactura"/> se dejan como vienen (el posteo ya las rechazó). Puede lanzar las excepciones de
    /// <see cref="CalculadoraIvaFactura.Calcular"/> si el IVA es incoherente (el posteo lo valida antes).
    /// </summary>
    public static Resultado Calcular(
        IReadOnlyList<LineaNotaAPostear> lineas,
        IReadOnlyDictionary<long, LineaFacturaVenta> lineasFactura,
        IReadOnlyDictionary<long, AcreditadoLinea> acreditado,
        IReadOnlyDictionary<string, IvaFacturaGrupo> ivaFactura)
    {
        var ajustadas = new List<LineaNotaAPostear>(lineas.Count);
        foreach (var l in lineas)
        {
            if (!lineasFactura.TryGetValue(l.LineaFacturaVentaId, out var o))
            {
                ajustadas.Add(l);
                continue;
            }

            var (importe, descuento) = TopeLinea(o, Acreditado(acreditado, o.Id), l.Cantidad);
            ajustadas.Add(l with { ImporteLinea = importe, ImporteDescuentoLinea = descuento });
        }

        var cantidadNota = lineas.GroupBy(l => l.LineaFacturaVentaId).ToDictionary(g => g.Key, g => g.Sum(l => l.Cantidad));
        bool Agotada(LineaFacturaVenta o) => Acreditado(acreditado, o.Id).Cantidad + cantidadNota.GetValueOrDefault(o.Id) >= o.Cantidad;
        var acreditables = lineasFactura.Values.Where(o => o.Tipo != TipoLineaFactura.Comentario).ToList();
        var agotaFactura = acreditables.All(Agotada);
        var gruposAgotados = acreditables
            .Where(o => o.IdentificadorIva is not null)
            .GroupBy(o => o.IdentificadorIva!, StringComparer.Ordinal)
            .Where(g => g.All(Agotada))
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

        var calculados = CalculadoraIvaFactura.Calcular(
            [.. ajustadas.Select(l => new LineaCalculoIva(l.NumeroLinea, l.IdentificadorIva!, l.PorcentajeIva, l.ImporteLinea))]);
        return new Resultado(ajustadas, TopeIva(calculados, ivaFactura, gruposAgotados, agotaFactura), agotaFactura);
    }

    /// <summary>Importe y descuento de la línea recalculados con el precio y el descuento de la factura y topados (ver la clase).</summary>
    public static (decimal Importe, decimal Descuento) TopeLinea(LineaFacturaVenta o, AcreditadoLinea a, decimal cantidad)
    {
        var remanenteImporte = Math.Max(0m, o.ImporteLinea - a.ImporteLinea);
        var remanenteDescuento = Math.Max(0m, o.ImporteDescuentoLinea - a.ImporteDescuentoLinea);
        if (a.Cantidad + cantidad >= o.Cantidad)
        {
            return (remanenteImporte, remanenteDescuento);
        }

        if (!LineaFacturaVentaBorradorReglas.TryCalcularImportes(
                cantidad, o.PrecioUnitario, o.PorcentajeDescuentoLinea, out var descuento, out var importe))
        {
            // Imposible con una cantidad menor que la facturada (la factura ya cupo): el tope lo resuelve.
            return (remanenteImporte, remanenteDescuento);
        }

        return (Math.Min(importe, remanenteImporte), Math.Min(descuento, remanenteDescuento));
    }

    /// <summary>IVA de cada grupo topado por el remanente del grupo en la factura (ver la clase).</summary>
    public static TotalesFactura TopeIva(
        TotalesFactura calculados, IReadOnlyDictionary<string, IvaFacturaGrupo> ivaFactura, IReadOnlySet<string> gruposAgotados, bool agotaFactura)
    {
        static decimal Remanente(IvaFacturaGrupo g) => Math.Max(0m, g.ImporteIva - g.IvaAcreditado);

        var grupos = calculados.Grupos
            .Select(g => ivaFactura.TryGetValue(g.IdentificadorIva, out var f)
                ? g with { ImporteIva = gruposAgotados.Contains(g.IdentificadorIva) ? Remanente(f) : Math.Min(g.ImporteIva, Remanente(f)) }
                : g)
            .ToList();

        if (agotaFactura)
        {
            // Grupos que la nota no trae pero a los que aún les queda IVA (redondeos de notas anteriores): remanente con base 0.
            foreach (var (identificador, f) in ivaFactura.Where(p => Remanente(p.Value) != 0m && grupos.All(g => g.IdentificadorIva != p.Key)))
            {
                grupos.Add(new GrupoIvaCalculado(identificador, f.PorcentajeIva, 0m, Remanente(f)));
            }

            grupos = [.. grupos.OrderBy(g => g.IdentificadorIva, StringComparer.Ordinal)];
        }

        var sinIva = grupos.Sum(g => g.BaseImponible);
        var iva = grupos.Sum(g => g.ImporteIva);
        return new TotalesFactura(grupos, sinIva, iva, sinIva + iva);
    }

    /// <summary>
    /// Importe de costo (positivo) de una devolución: <paramref name="calculado"/> topado por el valor de la salida original menos lo
    /// ya devuelto de la línea; el remanente exacto si la devolución agota la cantidad facturada.
    /// </summary>
    public static decimal TopeCosto(decimal calculado, CostoSalidaVenta salida, AcreditadoLinea a, decimal cantidad, decimal cantidadFacturada)
    {
        var remanente = Math.Max(0m, -salida.ImporteCosto - a.CostoDevuelto);
        return a.CantidadDevuelta + cantidad >= cantidadFacturada ? remanente : Math.Min(calculado, remanente);
    }

    private static AcreditadoLinea Acreditado(IReadOnlyDictionary<long, AcreditadoLinea> acreditado, long lineaId) =>
        acreditado.TryGetValue(lineaId, out var a) ? a : AcreditadoLinea.Ninguno;
}
