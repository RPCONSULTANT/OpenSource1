namespace OpenSource1.Core.Entities.Clientes;

/// <summary>
/// Serie <c>COBRO</c> de los pagos de clientes (Task 6.5), sin huecos, sembrada con <c>HasData</c> e Ids fijos igual que
/// <see cref="Ventas.SerieFacturaVentaIds"/> y <see cref="Contabilidad.SerieContabilidadIds"/>. Su número es el
/// <c>NumeroDocumento</c> del movimiento de cliente Pago y la <c>ClaveOrigen</c> del movimiento y de su asiento.
/// </summary>
public static class SerieCobroIds
{
    public const string Codigo = "COBRO";

    public static readonly Guid SerieId = Guid.Parse("e1000000-0000-0000-0000-000000000009");
    public static readonly Guid LineaSerieId = Guid.Parse("e1000000-0000-0000-0000-00000000000a");
}
