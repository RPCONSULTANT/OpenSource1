using System.Globalization;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.FacturasVenta.Borradores;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Ventas;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.NotasCreditoVenta;

/// <summary>Errores comunes de las notas de crédito (404 solo para el recurso de la RUTA).</summary>
internal static class NotaCreditoVentaErrores
{
    public const int LongitudDescripcion = 200;

    public static Error BorradorNoEncontrado(string campo = "Id") =>
        new("nota_credito_borrador.no_encontrado", "No se encontró el borrador de nota de crédito solicitado.", campo);

    /// <summary>El borrador está <see cref="EstadoNotaCreditoBorrador.Posteada"/>: de solo lectura (409).</summary>
    public static Error Posteada(string campo = "Id") =>
        new("nota_credito_borrador.posteada.conflicto", "El borrador de nota de crédito ya se posteó: es de solo lectura (abra su nota).", campo);

    public static Error LineaNoEncontrada() =>
        new("nota_credito_linea.no_encontrado", "No se encontró la línea de nota de crédito solicitada.", "Id");

    public static Error FacturaInvalida() =>
        new("nota_credito.factura_invalida", "La factura de venta posteada indicada no existe.", "FacturaVentaNumero");

    public static Error FechaAnteriorAFactura(DateOnly fechaFactura) => new(
        "nota_credito.fecha_invalida",
        $"La fecha de registro de la nota de crédito no puede ser anterior a la de su factura ({fechaFactura:dd/MM/yyyy}).",
        "FechaRegistro");

    public static Error? ValidarDescripcion(string? descripcion) =>
        descripcion is { Length: > LongitudDescripcion }
            ? new Error("nota_credito.descripcion_invalida", "La descripción admite como máximo 200 caracteres.", "Descripcion")
            : null;

    public static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Error de una línea con su número: <c>Lineas[n].Campo</c> y el prefijo "Línea n: ".</summary>
    public static Error DeLinea(int numeroLinea, Error original) => new(
        original.Codigo, $"Línea {numeroLinea}: {original.Mensaje}", $"Lineas[{numeroLinea}].{original.Campo}");
}

/// <summary>
/// Valores de una línea de nota: los copiados de la línea de la factura (no editables) más la cantidad, la devolución y los importes
/// calculados. Con devolución, además la cantidad en unidad base (convertida con el factor CONGELADO en la factura) y la unidad base.
/// </summary>
internal sealed record LineaNotaCalculada(
    LineaFacturaVenta Original,
    decimal Cantidad,
    bool DevolverInventario,
    decimal ImporteDescuentoLinea,
    decimal ImporteLinea,
    decimal? CantidadBase,
    Guid? UnidadBaseId)
{
    public void Aplicar(LineaNotaCreditoVentaBorrador linea)
    {
        linea.LineaFacturaVentaId = Original.Id;
        linea.NumeroLinea = Original.NumeroLinea;
        linea.Tipo = Original.Tipo;
        linea.ProductoId = Original.ProductoId;
        linea.CuentaContableId = Original.CuentaContableId;
        linea.Descripcion = Original.Descripcion;
        linea.AlmacenId = Original.AlmacenId;
        linea.UnidadMedidaId = Original.UnidadMedidaId;
        linea.CantidadPorUnidadMedida = Original.CantidadPorUnidadMedida;
        linea.Cantidad = Cantidad;
        linea.PrecioUnitario = Original.PrecioUnitario;
        linea.PorcentajeDescuentoLinea = Original.PorcentajeDescuentoLinea;
        linea.ImporteDescuentoLinea = ImporteDescuentoLinea;
        linea.ImporteLinea = ImporteLinea;
        linea.GrupoProductoId = Original.GrupoProductoId;
        linea.GrupoIvaProductoId = Original.GrupoIvaProductoId;
        linea.GrupoInventarioId = Original.GrupoInventarioId;
        linea.IdentificadorIva = Original.IdentificadorIva;
        linea.PorcentajeIva = Original.PorcentajeIva;
        linea.DevolverInventario = DevolverInventario;
    }
}

/// <summary>
/// Reglas de una línea de nota de crédito, compartidas por la captura (alta/modificación de líneas y la copia al crear el borrador)
/// y por la revalidación del posteo (bajo el bloqueo de la factura, con lo acreditado vigente). Como mucho un error por línea.
/// </summary>
internal static class LineaNotaCreditoVentaReglas
{
    /// <summary>
    /// <list type="number">
    /// <item>La línea de la factura es de Producto o CuentaContable (<c>nota_credito.linea_no_acreditable</c>).</item>
    /// <item>Cantidad &gt; 0, con 6 decimales como máximo (<c>nota_credito.cantidad_invalida</c>) y ≤ facturada − ya acreditada por notas
    /// posteadas (<c>nota_credito.cantidad_excede</c>).</item>
    /// <item>Devolución solo en líneas de Producto (<c>nota_credito.devolucion_invalida</c>).</item>
    /// <item>Importes con el precio y el descuento de la factura: <c>ImporteLinea = ROUND(Cantidad × Precio, 2) − ROUND(Cantidad ×
    /// Precio × % / 100, 2)</c>; un importe 0 solo con 100 % de descuento (<c>nota_credito.importe_invalido</c>, regla de la Task 8.4).</item>
    /// <item>Con devolución: el producto existe y la cantidad, convertida a la unidad base con el factor CONGELADO en la factura (el
    /// de la salida original), es exacta con los decimales ACTUALES de la unidad base (<c>conversion.cantidad_no_exacta</c>).</item>
    /// </list>
    /// </summary>
    public static async Task<Result<LineaNotaCalculada>> ValidarAsync(
        IUnitOfWork unitOfWork,
        IConversionUnidadMedidaService conversion,
        LineaFacturaVenta original,
        decimal acreditada,
        decimal? cantidad,
        bool devolverInventario,
        CancellationToken cancellationToken)
    {
        if (original.Tipo == TipoLineaFactura.Comentario)
        {
            return Fallo("nota_credito.linea_no_acreditable", "Una línea de comentario de la factura no se puede acreditar.", "LineaFacturaVentaId");
        }

        if (cantidad is not { } valor || valor <= 0m || valor > LineaFacturaVentaBorradorReglas.CantidadMaxima || decimal.Round(valor, 6) != valor)
        {
            return Fallo("nota_credito.cantidad_invalida", "La cantidad debe ser mayor que cero y tener como máximo 6 decimales.", "Cantidad");
        }

        var pendiente = original.Cantidad - acreditada;
        if (valor > pendiente)
        {
            return Fallo(
                "nota_credito.cantidad_excede",
                $"La cantidad {F(valor)} excede lo pendiente de acreditar de la línea {original.NumeroLinea} de la factura: facturado " +
                $"{F(original.Cantidad)}, ya acreditado {F(acreditada)}, pendiente {F(pendiente)}.",
                "Cantidad");
        }

        if (devolverInventario && original.Tipo != TipoLineaFactura.Producto)
        {
            return Fallo("nota_credito.devolucion_invalida", "Solo una línea de producto puede devolver inventario.", "DevolverInventario");
        }

        if (!LineaFacturaVentaBorradorReglas.TryCalcularImportes(
                valor, original.PrecioUnitario, original.PorcentajeDescuentoLinea, out var importeDescuento, out var importeLinea))
        {
            return Fallo("nota_credito.importe_invalido", "El importe de la línea es demasiado grande.", "Cantidad");
        }

        if (importeLinea == 0m && original.PorcentajeDescuentoLinea != 100m)
        {
            return Fallo(
                "nota_credito.importe_invalido",
                "El importe de la línea resultante es 0: aumente la cantidad (solo una línea al 100 % de descuento puede acreditarse por 0).",
                "Cantidad");
        }

        if (!devolverInventario)
        {
            return Result<LineaNotaCalculada>.Exito(new LineaNotaCalculada(original, valor, false, importeDescuento, importeLinea, null, null));
        }

        var producto = original.ProductoId is { } productoId
            ? await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(x => x.Id == productoId, cancellationToken: cancellationToken)
            : null;
        if (producto is null)
        {
            return Fallo("nota_credito.producto_invalido", "El producto de la línea ya no existe: no se puede devolver al inventario.", "ProductoId");
        }

        var conversionBase = await conversion.ObtenerConversionAsync(producto.Id, producto.UnidadMedidaBaseId, cancellationToken);
        if (!conversionBase.TryObtenerValor(out var baseUnidad))
        {
            var error = conversionBase.Errores[0];
            return Fallo(error.Codigo, error.Mensaje, "UnidadMedidaId");
        }

        // El factor CONGELADO en la factura (el de la salida original), no el vigente: se devuelve lo mismo que salió.
        var cantidadBase = new ConversionUnidadMedida(original.CantidadPorUnidadMedida, baseUnidad.DecimalesBase, baseUnidad.CodigoUnidadBase)
            .ConvertirExacta(valor);
        if (!cantidadBase.TryObtenerValor(out var enBase))
        {
            return Result<LineaNotaCalculada>.Fallo(cantidadBase.Errores[0] with { Campo = "Cantidad" });
        }

        return Result<LineaNotaCalculada>.Exito(new LineaNotaCalculada(
            original, valor, true, importeDescuento, importeLinea, enBase, producto.UnidadMedidaBaseId));
    }

    private static string F(decimal valor) => valor.ToString("0.######", CultureInfo.InvariantCulture);

    private static Result<LineaNotaCalculada> Fallo(string codigo, string mensaje, string campo) =>
        Result<LineaNotaCalculada>.Fallo(new Error(codigo, mensaje, campo));
}
