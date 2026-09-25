using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.Productos.Commands;

/// <summary>
/// Datos editables de un producto, comunes a Create y Update (en Update, ya resueltos contra los valores
/// guardados). Excluye a propósito <c>CostoUnitario</c> y <c>CostoAjustado</c>: los mantiene el sistema
/// (rutina de costeo de la Fase 3) y el cliente HTTP nunca los fija.
/// </summary>
public interface IDatosProducto
{
    string Codigo { get; }
    string Nombre { get; }
    decimal PrecioVenta { get; }
    int Stock { get; }
    MetodoCosteo MetodoCosteo { get; }
    decimal CostoEstandar { get; }
    BloqueoProducto Bloqueado { get; }
    string? ImagePath { get; }
}
