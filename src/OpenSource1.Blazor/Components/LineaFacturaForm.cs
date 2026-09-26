using System.ComponentModel.DataAnnotations;
using OpenSource1.Blazor.Services;
using OpenSource1.Core.Enums;

namespace OpenSource1.Blazor.Components;

/// <summary>
/// Formulario de una línea de borrador de factura (alta y modificación, Task 6.6). Los importes viajan como texto y se leen con
/// <see cref="EntradaDecimal"/> (independiente de la cultura). La validación depende de <see cref="Tipo"/> (campo oculto que fija
/// la página) y <see cref="ToInput"/> envía SOLO los campos del tipo: la API rechaza los demás (<c>factura.campo_no_aplica</c>).
/// </summary>
public sealed class LineaFacturaForm : IValidatableObject
{
    public const int LongitudDescripcion = 200;

    public short Tipo { get; set; } = (short)TipoLineaFactura.Producto;

    /// <summary>Solo Producto; vacío = el almacén de la cabecera.</summary>
    public Guid? AlmacenId { get; set; }

    /// <summary>Solo CuentaContable (obligatoria).</summary>
    public Guid? CuentaContableId { get; set; }

    /// <summary>Solo CuentaContable (obligatorio).</summary>
    public Guid? GrupoIvaProductoId { get; set; }

    public string? CantidadTexto { get; set; }

    /// <summary>Producto: vacío = <c>PrecioVenta × factor</c> (lo calcula la API). CuentaContable: obligatorio.</summary>
    public string? PrecioUnitarioTexto { get; set; }

    public string? PorcentajeDescuentoTexto { get; set; }

    public string? Descripcion { get; set; }

    public long Xmin { get; set; }

    /// <summary>Cuerpo hacia la API para <paramref name="tipo"/>; los campos que no aplican van nulos aunque el POST los traiga.</summary>
    public LineaFacturaInput ToInput(TipoLineaFactura tipo, Guid? productoId, Guid? unidadMedidaId)
    {
        var descripcion = string.IsNullOrWhiteSpace(Descripcion) ? null : Descripcion.Trim();
        return tipo switch
        {
            TipoLineaFactura.Comentario => new LineaFacturaInput(tipo, null, null, descripcion, null, null, null, null, null, null),
            TipoLineaFactura.CuentaContable => new LineaFacturaInput(
                tipo, null, CuentaContableId, descripcion, null, null, Decimal(CantidadTexto), Decimal(PrecioUnitarioTexto),
                Decimal(PorcentajeDescuentoTexto), GrupoIvaProductoId),
            _ => new LineaFacturaInput(
                TipoLineaFactura.Producto, productoId, null, descripcion, AlmacenId is { } a && a != Guid.Empty ? a : null, unidadMedidaId,
                Decimal(CantidadTexto), Decimal(PrecioUnitarioTexto), Decimal(PorcentajeDescuentoTexto), null),
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Descripcion is { } d && d.Trim().Length > LongitudDescripcion)
        {
            yield return new ValidationResult($"La descripción admite como máximo {LongitudDescripcion} caracteres.", [nameof(Descripcion)]);
        }

        var tipo = (TipoLineaFactura)Tipo;
        if (tipo == TipoLineaFactura.Comentario)
        {
            if (string.IsNullOrWhiteSpace(Descripcion))
            {
                yield return new ValidationResult("La línea de comentario requiere una descripción.", [nameof(Descripcion)]);
            }

            yield break;
        }

        if (tipo == TipoLineaFactura.CuentaContable)
        {
            if (CuentaContableId is null || CuentaContableId == Guid.Empty)
            {
                yield return new ValidationResult("Seleccione la cuenta contable.", [nameof(CuentaContableId)]);
            }

            if (GrupoIvaProductoId is null || GrupoIvaProductoId == Guid.Empty)
            {
                yield return new ValidationResult("Seleccione el grupo de IVA de producto.", [nameof(GrupoIvaProductoId)]);
            }
        }

        if (string.IsNullOrWhiteSpace(CantidadTexto))
        {
            yield return new ValidationResult("La cantidad es obligatoria.", [nameof(CantidadTexto)]);
        }
        else if (!EntradaDecimal.TryParse(CantidadTexto, out var cantidad, out var errorCantidad, "La cantidad"))
        {
            yield return new ValidationResult(errorCantidad, [nameof(CantidadTexto)]);
        }
        else if (cantidad <= 0)
        {
            yield return new ValidationResult("La cantidad debe ser mayor que cero.", [nameof(CantidadTexto)]);
        }

        if (string.IsNullOrWhiteSpace(PrecioUnitarioTexto))
        {
            if (tipo == TipoLineaFactura.CuentaContable)
            {
                yield return new ValidationResult("El precio unitario es obligatorio en una línea de cuenta contable.", [nameof(PrecioUnitarioTexto)]);
            }
        }
        else if (!EntradaDecimal.TryParse(PrecioUnitarioTexto, out var precio, out var errorPrecio, "El precio unitario"))
        {
            yield return new ValidationResult(errorPrecio, [nameof(PrecioUnitarioTexto)]);
        }
        else if (precio < 0)
        {
            yield return new ValidationResult("El precio unitario no puede ser negativo.", [nameof(PrecioUnitarioTexto)]);
        }

        if (!string.IsNullOrWhiteSpace(PorcentajeDescuentoTexto))
        {
            if (!EntradaDecimal.TryParse(PorcentajeDescuentoTexto, out var descuento, out var errorDescuento, "El porcentaje de descuento"))
            {
                yield return new ValidationResult(errorDescuento, [nameof(PorcentajeDescuentoTexto)]);
            }
            else if (descuento is < 0 or > 100)
            {
                yield return new ValidationResult("El porcentaje de descuento debe estar entre 0 y 100.", [nameof(PorcentajeDescuentoTexto)]);
            }
        }
    }

    private static decimal? Decimal(string? texto) =>
        !string.IsNullOrWhiteSpace(texto) && EntradaDecimal.TryParse(texto, out var valor, out _) ? valor : null;
}
