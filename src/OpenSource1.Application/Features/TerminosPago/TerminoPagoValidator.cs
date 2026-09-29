using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.TerminosPago;

/// <summary>
/// Validación compartida entre Create/Update: TerminoPago es una entidad plana sin value
/// objects, así que las reglas viven aquí en vez de en un VO con Of(...).
/// </summary>
internal static class TerminoPagoValidator
{
    public static IReadOnlyList<Error> Validar(
        string codigo, string descripcion, int diasVencimiento, int diasDescuento, decimal porcentajeDescuento)
    {
        var errores = new List<Error>();

        if (string.IsNullOrWhiteSpace(codigo))
        {
            errores.Add(new Error("termino_pago.codigo_requerido", "El código es obligatorio.", "Codigo"));
        }
        else if (codigo.Trim().Length > 20)
        {
            errores.Add(new Error("termino_pago.codigo_invalido", "El código no puede superar los 20 caracteres.", "Codigo"));
        }

        if (string.IsNullOrWhiteSpace(descripcion))
        {
            errores.Add(new Error("termino_pago.descripcion_requerida", "La descripción es obligatoria.", "Descripcion"));
        }
        else if (descripcion.Trim().Length > 200)
        {
            errores.Add(new Error("termino_pago.descripcion_invalida", "La descripción no puede superar los 200 caracteres.", "Descripcion"));
        }

        if (diasVencimiento < 0)
        {
            errores.Add(new Error("termino_pago.dias_vencimiento_invalido", "Los días de vencimiento no pueden ser negativos.", "DiasVencimiento"));
        }

        if (diasDescuento < 0)
        {
            errores.Add(new Error("termino_pago.dias_descuento_invalido", "Los días de descuento no pueden ser negativos.", "DiasDescuento"));
        }

        if (porcentajeDescuento < 0 || porcentajeDescuento > 100)
        {
            errores.Add(new Error("termino_pago.porcentaje_descuento_invalido", "El porcentaje de descuento debe estar entre 0 y 100.", "PorcentajeDescuento"));
        }

        return errores;
    }
}
