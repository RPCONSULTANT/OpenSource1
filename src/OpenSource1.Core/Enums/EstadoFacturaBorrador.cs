namespace OpenSource1.Core.Enums;

/// <summary>Estado de un borrador de factura de venta (Fase 6). Se guarda como <c>smallint</c>.</summary>
public enum EstadoFacturaBorrador : short
{
    /// <summary>Editable: admite cambios de cabecera y de líneas.</summary>
    Abierta = 1,

    /// <summary>Liberada: no admite cambios de cabecera ni de líneas hasta reabrirse.</summary>
    Liberada = 2,

    /// <summary>Posteada (spec no-series): el borrador se conserva enlazado a su factura y es de solo lectura.</summary>
    Posteada = 3,
}
