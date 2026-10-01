namespace OpenSource1.Core.Enums;

/// <summary>
/// Tipo de documento que numera una serie (spec no-series, Parte 1). Se guarda como smallint (CK 1–8): nunca renumerar.
/// </summary>
public enum TipoDocumentoSerie : short
{
    BorradorFacturaVenta = 1,
    FacturaVenta = 2,
    BorradorNotaCreditoVenta = 3,
    NotaCreditoVenta = 4,
    Cobro = 5,
    AsientoContable = 6,
    Cliente = 7,
    DiarioInventario = 8,
}

/// <summary>Rótulos en español de <see cref="TipoDocumentoSerie"/> (API y UI).</summary>
public static class TipoDocumentoSerieNombres
{
    public static IReadOnlyList<TipoDocumentoSerie> Todos { get; } = Enum.GetValues<TipoDocumentoSerie>();

    public static bool EsValido(TipoDocumentoSerie tipo) => Enum.IsDefined(tipo);

    public static string Nombre(TipoDocumentoSerie tipo) => tipo switch
    {
        TipoDocumentoSerie.BorradorFacturaVenta => "Borrador de factura de venta",
        TipoDocumentoSerie.FacturaVenta => "Factura de venta",
        TipoDocumentoSerie.BorradorNotaCreditoVenta => "Borrador de nota de crédito",
        TipoDocumentoSerie.NotaCreditoVenta => "Nota de crédito de venta",
        TipoDocumentoSerie.Cobro => "Cobro de cliente",
        TipoDocumentoSerie.AsientoContable => "Asiento contable",
        TipoDocumentoSerie.Cliente => "Código de cliente",
        TipoDocumentoSerie.DiarioInventario => "Diario de inventario",
        _ => $"Tipo {(short)tipo}",
    };
}
