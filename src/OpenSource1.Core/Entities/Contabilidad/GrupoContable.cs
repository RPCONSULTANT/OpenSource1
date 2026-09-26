namespace OpenSource1.Core.Entities.Contabilidad;

/// <summary>
/// Forma mínima común de los grupos contables (spec 5.2): <c>Codigo varchar(20)</c> único (índice parcial
/// <c>"IsDeleted" = false</c>) y <c>Descripcion varchar(100)</c>. Están casi vacíos a propósito: la contabilidad vive en las
/// intersecciones (setups, Task 5.4), no en los grupos. Cada subtipo es su PROPIA tabla (no hay herencia mapeada en EF: la
/// base no es una entidad del modelo, igual que <see cref="BaseEntity"/>).
/// </summary>
public abstract class GrupoContable : BaseEntity
{
    /// <summary>Código en mayúsculas: letras, números, guion y guion bajo; 1-20 caracteres.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>Grupo contable de negocio del socio (<c>NACIONAL</c>, <c>EXTERIOR</c>): eje del setup general con el grupo de producto.</summary>
public sealed class GrupoNegocio : GrupoContable;

/// <summary>Grupo contable de producto (<c>BIENES</c>, <c>SERVICIOS</c>): determina la cuenta de resultado (ventas, costo de ventas).</summary>
public sealed class GrupoProducto : GrupoContable;

/// <summary>Grupo de IVA del socio (<c>ITBIS18</c>, <c>EXENTO</c>): eje del setup de IVA con el grupo de IVA del producto.</summary>
public sealed class GrupoIvaNegocio : GrupoContable;

/// <summary>Grupo de IVA del producto (<c>ITBIS18</c>, <c>EXENTO</c>): determina la tasa y la cuenta de impuesto.</summary>
public sealed class GrupoIvaProducto : GrupoContable;

/// <summary>Grupo de inventario del producto (<c>GENERAL</c>): determina la cuenta de activo, cruzado con el almacén.</summary>
public sealed class GrupoInventario : GrupoContable;

/// <summary>
/// Grupo contable de cliente (spec 5.2): la excepción que SÍ lleva cuentas, porque su lookup es unidimensional.
/// <see cref="CuentaCxCId"/> es obligatoria y debe ser una cuenta de Posteo no bloqueada; las otras dos son opcionales.
/// </summary>
public sealed class GrupoClienteContable : GrupoContable
{
    public Guid CuentaCxCId { get; set; }

    public Guid? CuentaDescuentoId { get; set; }

    public Guid? CuentaInteresId { get; set; }
}

/// <summary>
/// Ids fijos de los grupos semilla (Task 5.3), <c>f2000000-0000-0000-0000-0000000000NN</c>. Son también los grupos por
/// defecto que la migración <c>AddGruposContables</c> asigna a los productos (<c>BIENES</c>, <c>ITBIS18</c>, <c>GENERAL</c>) y a
/// los socios (<c>NACIONAL</c>, <c>ITBIS18</c>, <c>GENERAL</c>) ya existentes. La Task 5.4 (setups) los referencia por estas constantes.
/// </summary>
public static class GrupoContableIds
{
    public static readonly Guid NegocioNacional = Guid.Parse("f2000000-0000-0000-0000-000000000001");
    public static readonly Guid NegocioExterior = Guid.Parse("f2000000-0000-0000-0000-000000000002");
    public static readonly Guid ProductoBienes = Guid.Parse("f2000000-0000-0000-0000-000000000003");
    public static readonly Guid ProductoServicios = Guid.Parse("f2000000-0000-0000-0000-000000000004");
    public static readonly Guid IvaNegocioItbis18 = Guid.Parse("f2000000-0000-0000-0000-000000000005");
    public static readonly Guid IvaNegocioExento = Guid.Parse("f2000000-0000-0000-0000-000000000006");
    public static readonly Guid IvaProductoItbis18 = Guid.Parse("f2000000-0000-0000-0000-000000000007");
    public static readonly Guid IvaProductoExento = Guid.Parse("f2000000-0000-0000-0000-000000000008");
    public static readonly Guid InventarioGeneral = Guid.Parse("f2000000-0000-0000-0000-000000000009");
    public static readonly Guid ClienteContableGeneral = Guid.Parse("f2000000-0000-0000-0000-000000000010");
}
