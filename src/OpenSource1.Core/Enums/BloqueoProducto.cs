namespace OpenSource1.Core.Enums;

/// <summary>Nivel de bloqueo del producto (Venta = no se puede vender; Todo = ningún movimiento). Se guarda como <c>smallint</c>.</summary>
public enum BloqueoProducto : short
{
    Ninguno = 0,
    Venta = 1,
    Todo = 2
}
