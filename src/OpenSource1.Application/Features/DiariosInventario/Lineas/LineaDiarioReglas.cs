using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Features.DiariosInventario.Lineas;

/// <summary>
/// Validación de una línea de diario al capturarla (Create/Update), en el orden exacto del brief de la
/// Task 4.2: lote -> tipo de movimiento -> producto/almacén -> almacén destino -> cantidad -> costo ->
/// unidad (factor) -> longitudes de texto. Fail-fast: devuelve el primer error, no una lista.
/// <c>NO</c> valida existencia suficiente (eso lo hace el registro, Task 4.3).
/// </summary>
internal static class LineaDiarioReglas
{
    /// <summary>Máximo de <c>numeric(18,4)</c>, mismo criterio que <c>RegistroMovimientosInventario</c>.</summary>
    private const decimal ImporteMaximo = 99_999_999_999_999.9999m;

    /// <summary>Máximo de <c>numeric(18,6)</c> (12 dígitos enteros + 6 decimales): límite de <c>Cantidad</c> y de la
    /// cantidad ya convertida a la unidad base del producto (<c>Cantidad × factor</c>).</summary>
    private const decimal CantidadMaxima = 999_999_999_999.999999m;

    private static readonly TipoMovimientoInventario[] TiposArticulo =
        [TipoMovimientoInventario.AjustePositivo, TipoMovimientoInventario.AjusteNegativo];

    private static readonly TipoMovimientoInventario[] TiposReclasificacion = [TipoMovimientoInventario.Transferencia];

    public static async Task<Result<LineaDiarioCalculo>> ValidarYCalcularAsync(
        IUnitOfWork unitOfWork, IConversionUnidadMedidaService conversion, LineaDiarioDatos datos, CancellationToken cancellationToken)
    {
        // 0. Fechas obligatorias: un DateOnly no puede ser null, así que un cuerpo que las omite las deserializa como
        // default(DateOnly) (0001-01-01) en vez de lanzar; hay que rechazarlo explícitamente.
        if (datos.FechaRegistro == default)
        {
            return Fallo("diario.fecha_invalida", "La fecha de registro es obligatoria.", "FechaRegistro");
        }

        if (datos.FechaDocumento == default)
        {
            return Fallo("diario.fecha_invalida", "La fecha de documento es obligatoria.", "FechaDocumento");
        }

        // 1. Lote: existencia y bloqueo son códigos DISTINTOS (a diferencia del POST/PUT de línea, donde
        // LoteDiarioId no es una referencia del cuerpo: en el POST viene de la URL -> 404 si no existe; el bloqueo
        // sigue siendo una regla de negocio -> 400).
        var lote = await unitOfWork.Repository<LoteDiario>().FirstOrDefaultAsync(
            x => x.Id == datos.LoteDiarioId, cancellationToken: cancellationToken);
        if (lote is null)
        {
            return Fallo("diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "LoteDiarioId");
        }

        if (lote.Bloqueado)
        {
            return Fallo("diario.lote_bloqueado", "El lote está bloqueado.", "LoteDiarioId");
        }

        var plantilla = await unitOfWork.Repository<PlantillaDiario>().FirstOrDefaultAsync(
            x => x.Id == lote.PlantillaDiarioId, cancellationToken: cancellationToken);
        if (plantilla is null)
        {
            return Fallo("diario_lote.no_encontrado", "No se encontró el lote de diario solicitado.", "LoteDiarioId");
        }

        // 2. TipoMovimiento permitido por el tipo de la plantilla.
        var permitidos = plantilla.Tipo == TipoPlantillaDiario.Articulo ? TiposArticulo : TiposReclasificacion;
        if (!permitidos.Contains(datos.TipoMovimiento))
        {
            return Fallo(
                "diario.tipo_movimiento_invalido",
                "El tipo de movimiento no está permitido por la plantilla del lote.", "TipoMovimiento");
        }

        // 3. Producto existe y no bloqueado para todo movimiento.
        var producto = await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(
            x => x.Id == datos.ProductoId, cancellationToken: cancellationToken);
        if (producto is null || producto.Bloqueado == BloqueoProducto.Todo)
        {
            return Fallo("diario.producto_invalido", "El producto no existe o está bloqueado.", "ProductoId");
        }

        // 4. Almacén (origen) existe y no bloqueado.
        var almacen = await unitOfWork.Repository<Almacen>().FirstOrDefaultAsync(
            x => x.Id == datos.AlmacenId, cancellationToken: cancellationToken);
        if (almacen is null || almacen.Bloqueado)
        {
            return Fallo("diario.almacen_invalido", "El almacén no existe o está bloqueado.", "AlmacenId");
        }

        // 5. Almacén destino: obligatorio y distinto del origen SOLO en Transferencia; nulo en los demás.
        string? almacenDestinoCodigo = null;
        if (datos.TipoMovimiento == TipoMovimientoInventario.Transferencia)
        {
            if (datos.AlmacenDestinoId is null || datos.AlmacenDestinoId == datos.AlmacenId)
            {
                return Fallo(
                    "diario.almacen_destino_invalido",
                    "El almacén destino es obligatorio en una transferencia y debe ser distinto del origen.", "AlmacenDestinoId");
            }

            var destino = await unitOfWork.Repository<Almacen>().FirstOrDefaultAsync(
                x => x.Id == datos.AlmacenDestinoId, cancellationToken: cancellationToken);
            if (destino is null || destino.Bloqueado)
            {
                return Fallo("diario.almacen_invalido", "El almacén destino no existe o está bloqueado.", "AlmacenDestinoId");
            }

            almacenDestinoCodigo = destino.Codigo;
        }
        else if (datos.AlmacenDestinoId is not null)
        {
            return Fallo(
                "diario.almacen_destino_invalido",
                "El almacén destino solo aplica a una transferencia.", "AlmacenDestinoId");
        }

        // 6. Cantidad > 0, hasta 6 decimales, y dentro de numeric(18,6) (sin este límite, un valor cercano al máximo de
        // decimal desborda la columna en Postgres con 22003 numeric_field_overflow en vez de un 400 legible).
        if (datos.Cantidad <= 0 || datos.Cantidad > CantidadMaxima || decimal.Round(datos.Cantidad, 6) != datos.Cantidad)
        {
            return Fallo(
                "diario.cantidad_invalida",
                $"La cantidad debe ser mayor que cero, hasta {CantidadMaxima:0.######} y tener como máximo 6 decimales.", "Cantidad");
        }

        // 7. Costo unitario: obligatorio (>= 0, <= 4 decimales, < 1e14) en AjustePositivo; nulo en AjusteNegativo/Transferencia.
        if (datos.TipoMovimiento == TipoMovimientoInventario.AjustePositivo)
        {
            if (datos.CostoUnitario is null)
            {
                return Fallo("diario.costo_requerido", "El ajuste positivo requiere un costo unitario.", "CostoUnitario");
            }

            if (!EsImporteValido(datos.CostoUnitario.Value))
            {
                return Fallo(
                    "diario.costo_invalido",
                    "El costo unitario debe ser mayor o igual que cero, menor que 1e14 y tener como máximo 4 decimales.", "CostoUnitario");
            }
        }
        else if (datos.CostoUnitario is not null)
        {
            return Fallo("diario.costo_invalido", "El costo unitario solo aplica al ajuste positivo.", "CostoUnitario");
        }

        // 8. Unidad de medida: factor de conversión a la unidad base del producto (se congela al guardar).
        var conversionResultado = await conversion.ObtenerConversionAsync(datos.ProductoId, datos.UnidadMedidaId, cancellationToken);
        if (!conversionResultado.TryObtenerValor(out var conversionUnidad))
        {
            var original = conversionResultado.Errores[0];
            return Result<LineaDiarioCalculo>.Fallo(new Error(original.Codigo, original.Mensaje, "UnidadMedidaId"));
        }

        // El factor congelado (redondeado a 6) es el mismo con el que el registro convertirá. La cantidad en la unidad BASE
        // (Cantidad × factor) debe caber en numeric(18,6) (un factor grande puede desbordar la columna del libro aunque
        // Cantidad por sí sola sea válida) y ser EXACTA con los decimales de la unidad base: nunca se redondea (2.5 UND con
        // 0 decimales se rechaza; el inventario no puede decir 3 cuando el diario dice 2.5).
        var factor = conversionUnidad.Factor;
        var cantidadBaseResultado = conversionUnidad.ConvertirExacta(datos.Cantidad);
        if (cantidadBaseResultado.EsFallo)
        {
            var original = cantidadBaseResultado.Errores[0];
            var codigo = original.Codigo == "conversion.cantidad_no_exacta" ? original.Codigo : "diario.cantidad_invalida";
            return Fallo(codigo, original.Mensaje, "Cantidad");
        }

        // Defensivo: si ObtenerConversionAsync tuvo éxito, la unidad existe en el catálogo (es la base del producto, ya
        // comprobada arriba por el servicio, o está asociada vía UnidadesMedidaProducto con FK Restrict). Solo se
        // consulta aquí para obtener su Código para la respuesta.
        var unidadMedida = await unitOfWork.Repository<UnidadMedida>().FirstOrDefaultAsync(
            x => x.Id == datos.UnidadMedidaId, cancellationToken: cancellationToken);
        if (unidadMedida is null)
        {
            return Fallo("conversion.unidad_base_no_encontrada", "La unidad de medida no existe en el catálogo.", "UnidadMedidaId");
        }

        // 9. Longitudes de texto.
        if (datos.NumeroDocumento is { Length: > 20 })
        {
            return Fallo(
                "diario.numero_documento_invalido", "El número de documento admite como máximo 20 caracteres.", "NumeroDocumento");
        }

        if (datos.Descripcion is { Length: > 200 })
        {
            return Fallo("diario.descripcion_invalida", "La descripción admite como máximo 200 caracteres.", "Descripcion");
        }

        // ImporteCosto: calculado solo cuando hay costo (AjustePositivo); 0 en los demás (se determina al registrar).
        // Cada factor individual (Cantidad, CostoUnitario) ya pasó su propio límite, pero el PRODUCTO puede seguir
        // desbordando numeric(18,4) igual (p. ej. cantidad 1e6 × costo 1e10 = 1e16): se comprueba aparte.
        decimal importeCosto;
        if (datos.CostoUnitario is { } costo)
        {
            try
            {
                checked
                {
                    importeCosto = Math.Round(datos.Cantidad * factor * costo, 4, MidpointRounding.AwayFromZero);
                }
            }
            catch (OverflowException)
            {
                return Fallo("diario.costo_invalido", "El importe de costo resultante es demasiado grande.", "CostoUnitario");
            }

            if (Math.Abs(importeCosto) > ImporteMaximo)
            {
                return Fallo("diario.costo_invalido", "El importe de costo resultante es demasiado grande.", "CostoUnitario");
            }
        }
        else
        {
            importeCosto = 0m;
        }

        return Result<LineaDiarioCalculo>.Exito(new LineaDiarioCalculo(
            factor, importeCosto, producto.Codigo, producto.Nombre, almacen.Codigo, almacenDestinoCodigo, unidadMedida.Codigo));
    }

    private static bool EsImporteValido(decimal valor) =>
        valor >= 0 && valor <= ImporteMaximo && decimal.Round(valor, 4) == valor;

    private static Result<LineaDiarioCalculo> Fallo(string codigo, string mensaje, string campo) =>
        Result<LineaDiarioCalculo>.Fallo(new Error(codigo, mensaje, campo));
}
