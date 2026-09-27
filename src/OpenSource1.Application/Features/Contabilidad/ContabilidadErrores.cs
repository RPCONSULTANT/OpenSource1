using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.Contabilidad;

/// <summary>Errores comunes de las vistas contables (movimientos y balance de comprobación).</summary>
internal static class ContabilidadErrores
{
    public static Error RangoFechasInvalido() =>
        new("contabilidad.rango_fechas_invalido", "La fecha 'desde' no puede ser posterior a 'hasta'.", "Desde");
}
