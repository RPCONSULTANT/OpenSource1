namespace OpenSource1.Core.Enums;

/// <summary>
/// Los cinco grupos contables "simples" (solo <c>Codigo</c>/<c>Descripcion</c>) que comparten un único mantenimiento
/// genérico (Fase 5, Task 5.3): API <c>api/grupos-contables/{tipo}</c> y una página con selector de tipo. Cada tipo tiene
/// su propia tabla. <c>GruposClienteContable</c> NO está aquí: lleva cuentas y tiene su propio mantenimiento.
/// </summary>
public enum TipoGrupoContable : short
{
    Negocio = 1,
    Producto = 2,
    IvaNegocio = 3,
    IvaProducto = 4,
    Inventario = 5
}
