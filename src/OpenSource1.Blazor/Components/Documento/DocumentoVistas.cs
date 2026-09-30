using System.Globalization;
using Microsoft.AspNetCore.Components;
using OpenSource1.Application.Features.FacturasVenta.Borradores.Dtos;
using OpenSource1.Application.Features.FacturasVenta.Calculo;
using OpenSource1.Application.Features.FacturasVenta.Posteadas.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Borradores.Dtos;
using OpenSource1.Application.Features.NotasCreditoVenta.Posteadas.Dtos;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components.Documento;

/// <summary>
/// Un dato de la cabecera de un documento en solo lectura (spec no-series, Parte 4): <c>Href</c> lo convierte en enlace,
/// <c>Detalle</c> es una segunda línea tenue (RNC, dirección…), <c>Ancho</c> ocupa toda la fila (descripción) y <c>Contenido</c>,
/// si se indica, sustituye al valor (lo usa <see cref="CamposDocumento.Series"/>).
/// </summary>
public sealed record CampoDocumento(
    string Etiqueta, string Valor, string? Href = null, string? Detalle = null, bool Ancho = false, RenderFragment? Contenido = null);

/// <summary>Campos comunes de la cabecera en solo lectura.</summary>
public static class CamposDocumento
{
    /// <summary>
    /// Campo "Series" con <see cref="SerieNumeracionInfo"/> (I3: ÚNICA forma de mostrar la serie en modo lectura, en S13, S14 y S15).
    /// Borrador no Posteada: pasar el <see cref="ProximoVista"/>; Posteada o documento posteado: <paramref name="proximo"/> null.
    /// </summary>
    public static CampoDocumento Series(string? serieRegistroCodigo, ProximoVista? proximo = null) =>
        new("Series", serieRegistroCodigo ?? "—", Contenido: builder =>
        {
            builder.OpenComponent<SerieNumeracionInfo>(0);
            builder.AddComponentParameter(1, nameof(SerieNumeracionInfo.Codigo), serieRegistroCodigo);
            builder.AddComponentParameter(2, nameof(SerieNumeracionInfo.ProximoNumero), proximo?.Numero);
            builder.AddComponentParameter(3, nameof(SerieNumeracionInfo.Aviso), proximo?.Aviso);
            builder.AddComponentParameter(4, nameof(SerieNumeracionInfo.Error), proximo?.Error);
            builder.CloseComponent();
        });
}

/// <summary>
/// Totales al pie del documento: subtotal (antes de descuentos), descuentos, importe sin ITBIS, ITBIS y total, con el IVA agrupado
/// por identificador. <see cref="Desde"/> los arma desde los <see cref="TotalesFactura"/> de la API (vista previa del borrador o
/// factura/nota posteada) y la suma de descuentos de las líneas.
/// </summary>
public sealed record TotalesDocumento(
    decimal Subtotal, decimal Descuentos, decimal ImporteSinIva, decimal Itbis, decimal Total, IReadOnlyList<GrupoIvaCalculado> Grupos)
{
    public static TotalesDocumento Desde(TotalesFactura totales, decimal descuentos) =>
        new(totales.ImporteSinIva + descuentos, descuentos, totales.ImporteSinIva, totales.ImporteIva, totales.ImporteTotal, totales.Grupos);

    /// <summary>Totales de una factura posteada: importes de la cabecera, grupos de IVA guardados y descuentos de sus líneas.</summary>
    public static TotalesDocumento Desde(FacturaVentaDetalleResponse d) => Posteado(
        d.Cabecera.ImporteSinIva, d.Cabecera.ImporteIva, d.Cabecera.ImporteTotal, d.Lineas.Sum(l => l.ImporteDescuentoLinea),
        d.LineasIva.Select(g => new GrupoIvaCalculado(g.IdentificadorIva, g.PorcentajeIva, g.BaseImponible, g.ImporteIva)));

    /// <summary>Totales de una nota de crédito posteada (misma aritmética que la factura).</summary>
    public static TotalesDocumento Desde(NotaCreditoVentaDetalleResponse d) => Posteado(
        d.Cabecera.ImporteSinIva, d.Cabecera.ImporteIva, d.Cabecera.ImporteTotal, d.Lineas.Sum(l => l.ImporteDescuentoLinea),
        d.LineasIva.Select(g => new GrupoIvaCalculado(g.IdentificadorIva, g.PorcentajeIva, g.BaseImponible, g.ImporteIva)));

    /// <summary>El subtotal es el importe sin ITBIS más los descuentos de línea (los importes guardados ya los descuentan).</summary>
    private static TotalesDocumento Posteado(decimal importeSinIva, decimal importeIva, decimal importeTotal, decimal descuentos, IEnumerable<GrupoIvaCalculado> grupos) =>
        new(importeSinIva + descuentos, descuentos, importeSinIva, importeIva, importeTotal, [.. grupos]);
}

/// <summary>Variante de <see cref="DocumentoFilaFormulario"/>: <c>Alta</c> = <c>fila-nueva</c> (verde), <c>Edicion</c> = <c>fila-edicion</c> (ámbar).</summary>
public enum VarianteFilaFormulario
{
    Alta,
    Edicion,
}

/// <summary>Columna de <see cref="DocumentoLineasLectura{TLinea}"/>: título, valor (texto), alineación, clase de la celda y detalle tenue.</summary>
public sealed record ColumnaDocumento<TLinea>(
    string Titulo, Func<TLinea, string?> Valor, bool Derecha = false, string? Clase = null, Func<TLinea, string?>? Detalle = null);

/// <summary>
/// Línea de factura o nota en solo lectura (NS20), común a borradores y documentos posteados. <c>Devolucion</c> solo aplica a notas
/// (null en facturas). Las de comentario dejan vacías las columnas numéricas.
/// </summary>
public sealed record LineaDocumento(
    int NumeroLinea, TipoLineaFactura Tipo, string? Codigo, string? Descripcion, string? Almacen, decimal Cantidad, string? Unidad,
    decimal PrecioUnitario, decimal PorcentajeDescuento, decimal ImporteLinea, string? IdentificadorIva, decimal PorcentajeIva,
    bool? Devolucion = null)
{
    public bool EsComentario => Tipo == TipoLineaFactura.Comentario;

    public static LineaDocumento Desde(LineaFacturaVentaBorradorResponse l) => new(
        l.NumeroLinea, l.Tipo, l.ProductoCodigo ?? l.CuentaContableNumero, l.Descripcion, l.AlmacenCodigo, l.Cantidad, l.UnidadMedidaCodigo,
        l.PrecioUnitario, l.PorcentajeDescuentoLinea, l.ImporteLinea, l.IdentificadorIva, l.PorcentajeIva);

    public static LineaDocumento Desde(LineaFacturaVentaResponse l) => new(
        l.NumeroLinea, l.Tipo, l.ProductoCodigo ?? l.CuentaContableNumero, l.Descripcion, l.AlmacenCodigo, l.Cantidad, l.UnidadMedidaCodigo,
        l.PrecioUnitario, l.PorcentajeDescuentoLinea, l.ImporteLinea, l.IdentificadorIva, l.PorcentajeIva);

    public static LineaDocumento Desde(LineaNotaCreditoVentaBorradorResponse l) => new(
        l.NumeroLinea, l.Tipo, l.ProductoCodigo ?? l.CuentaContableNumero, l.Descripcion, l.AlmacenCodigo, l.Cantidad, l.UnidadMedidaCodigo,
        l.PrecioUnitario, l.PorcentajeDescuentoLinea, l.ImporteLinea, l.IdentificadorIva, l.PorcentajeIva, l.DevolverInventario);

    public static LineaDocumento Desde(LineaNotaCreditoVentaResponse l) => new(
        l.NumeroLinea, l.Tipo, l.ProductoCodigo ?? l.CuentaContableNumero, l.Descripcion, l.AlmacenCodigo, l.Cantidad, l.UnidadMedidaCodigo,
        l.PrecioUnitario, l.PorcentajeDescuentoLinea, l.ImporteLinea, l.IdentificadorIva, l.PorcentajeIva, l.DevolverInventario);
}

/// <summary>Los dos usos fijos de <see cref="DocumentoLineasLectura{TLinea}"/> (I2): líneas de factura (10 columnas) y de nota (11).</summary>
public static class ColumnasDocumento
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary># · Tipo · Producto / Cuenta · Descripción · Almacén · Cantidad (+unidad) · Precio · Desc. % · Importe · IVA.</summary>
    public static readonly IReadOnlyList<ColumnaDocumento<LineaDocumento>> Factura = Comunes();

    /// <summary>Las de <see cref="Factura"/> + Devolución (Sí/No).</summary>
    public static readonly IReadOnlyList<ColumnaDocumento<LineaDocumento>> Nota =
        [.. Comunes(), new("Devolución", l => l.EsComentario ? null : l.Devolucion == true ? "Sí" : "No", Clase: "text-xs text-slate-500")];

    private static List<ColumnaDocumento<LineaDocumento>> Comunes() =>
    [
        new("#", l => l.NumeroLinea.ToString(Inv), Clase: "text-xs text-slate-400"),
        new("Tipo", l => VentasOpciones.TipoLinea(l.Tipo), Clase: "text-xs text-slate-700"),
        new("Producto / Cuenta", l => l.Codigo ?? "—", Clase: "text-xs text-slate-700"),
        new("Descripción", l => l.Descripcion),
        new("Almacén", l => l.Almacen ?? "—", Clase: "text-xs text-slate-500"),
        new("Cantidad", l => l.EsComentario ? null : l.Cantidad.ToString("0.######", Inv), Derecha: true, Detalle: l => l.EsComentario ? null : l.Unidad),
        new("Precio", l => l.EsComentario ? null : l.PrecioUnitario.ToString("0.00##", Inv), Derecha: true),
        new("Desc. %", l => l.EsComentario ? null : l.PorcentajeDescuento == 0 ? "—" : l.PorcentajeDescuento.ToString("0.#####", Inv), Derecha: true, Clase: "text-slate-500"),
        new("Importe", l => l.EsComentario ? null : VentasOpciones.Importe(l.ImporteLinea), Derecha: true, Clase: "font-semibold text-slate-800"),
        new("IVA", l => l.EsComentario ? null : $"{l.IdentificadorIva} {l.PorcentajeIva.ToString("0.##", Inv)}%", Clase: "text-xs text-slate-500"),
    ];
}
