namespace OpenSource1.Core.Enums;

/// <summary>
/// Rol de una cuenta contable dentro del plan de cuentas. Solo las cuentas <see cref="Posteo"/>
/// reciben movimientos; las demás son de agrupación/presentación (Fase 5, Task 5.2). Se guarda
/// como <c>smallint</c>.
/// </summary>
public enum TipoCuentaContable : short
{
    Posteo = 1,
    Encabezado = 2,
    Total = 3,
    InicioTotal = 4,
    FinTotal = 5
}
