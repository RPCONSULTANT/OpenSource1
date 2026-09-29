using OpenSource1.Core.Enums;

namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Cuenta del plan de cuentas contables (Fase 5, Task 5.2). Entidad plana sin value objects, mismo
/// patrón que <see cref="Almacen"/>. Solo las cuentas <see cref="TipoCuentaContable.Posteo"/> reciben
/// movimientos del libro contable (Task 5.5); las demás (Encabezado/Total/InicioTotal/FinTotal) son
/// de agrupación y presentación del reporte del plan de cuentas.
/// </summary>
public sealed class CuentaContable : BaseEntity
{
    /// <summary>Número de cuenta, único (índice parcial <c>"IsDeleted" = false</c>). Solo dígitos, punto y guion; 1-20 caracteres.</summary>
    public required string Numero { get; set; }

    public required string Nombre { get; set; }

    public TipoCuentaContable TipoCuenta { get; set; }

    public TipoResultadoCuenta TipoResultado { get; set; }

    /// <summary>
    /// <see langword="true"/> si un usuario puede escribir líneas de diario contra esta cuenta
    /// directamente; <see langword="false"/> en las cuentas que solo toca el sistema (p. ej. CxC,
    /// Inventario, ITBIS por pagar, Costo de ventas), que reciben sus movimientos únicamente vía los
    /// motores de posteo de fases posteriores.
    /// </summary>
    public bool PosteoDirecto { get; set; }

    public bool Bloqueada { get; set; }

    /// <summary>Nivel de sangría visual en el listado del plan de cuentas (0-10).</summary>
    public int Sangria { get; set; }
}

/// <summary>
/// Ids fijos de las cuentas semilla del plan de cuentas (Task 5.2), en el mismo orden en que las
/// enumera el brief. Las Tasks 5.3-5.6 (setups contables, grupos de cliente, libro contable) las
/// referencian por estas constantes.
/// </summary>
public static class CuentaContableIds
{
    public static readonly Guid Activos = Guid.Parse("f1000000-0000-0000-0000-000000000001");
    public static readonly Guid Caja = Guid.Parse("f1000000-0000-0000-0000-000000000002");
    public static readonly Guid CxC = Guid.Parse("f1000000-0000-0000-0000-000000000003");
    public static readonly Guid Inventario = Guid.Parse("f1000000-0000-0000-0000-000000000004");
    public static readonly Guid Pasivos = Guid.Parse("f1000000-0000-0000-0000-000000000005");
    public static readonly Guid IvaPorPagar = Guid.Parse("f1000000-0000-0000-0000-000000000006");
    public static readonly Guid Ingresos = Guid.Parse("f1000000-0000-0000-0000-000000000007");
    public static readonly Guid Ventas = Guid.Parse("f1000000-0000-0000-0000-000000000008");
    public static readonly Guid DescuentoVentas = Guid.Parse("f1000000-0000-0000-0000-000000000009");
    public static readonly Guid Costos = Guid.Parse("f1000000-0000-0000-0000-000000000010");
    public static readonly Guid CostoVentas = Guid.Parse("f1000000-0000-0000-0000-000000000011");
    public static readonly Guid AjusteInventario = Guid.Parse("f1000000-0000-0000-0000-000000000012");
}
