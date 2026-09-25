namespace OpenSource1.Core.Enums;

/// <summary>
/// Tipo de plantilla de diario de inventario (Fase 4). Determina qué <see cref="TipoMovimientoInventario"/>
/// admiten las líneas de los lotes que cuelgan de ella: <c>Articulo</c> admite AjustePositivo/AjusteNegativo,
/// <c>Reclasificacion</c> admite solo Transferencia. Se guarda como <c>smallint</c>.
/// </summary>
public enum TipoPlantillaDiario : short
{
    Articulo = 1,
    Reclasificacion = 2
}
