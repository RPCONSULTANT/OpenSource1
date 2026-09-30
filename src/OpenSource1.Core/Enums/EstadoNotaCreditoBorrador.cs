namespace OpenSource1.Core.Enums;

/// <summary>Estado de un borrador de nota de crédito (spec no-series). Las notas no se liberan: Abierta o Posteada.</summary>
public enum EstadoNotaCreditoBorrador : short
{
    /// <summary>Editable.</summary>
    Abierta = 1,

    /// <summary>Posteada: se conserva enlazada a su nota y es de solo lectura.</summary>
    Posteada = 3,
}
