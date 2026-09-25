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

    private static readonly TipoMovimientoInventario[] TiposArticulo =
        [TipoMovimientoInventario.AjustePositivo, TipoMovimientoInventario.AjusteNegativo];

    private static readonly TipoMovimientoInventario[] TiposReclasificacion = [TipoMovimientoInventario.Transferencia];

    public static async Task<Result<LineaDiarioCalculo>> ValidarYCalcularAsync(
        IUnitOfWork unitOfWork, IConversionUnidadMedidaService conversion, LineaDiarioDatos datos, CancellationToken cancellationToken)
    {
        // 1. Lote existe y no bloqueado (código único para ambos casos: LoteDiarioId no es una referencia de la URL
        // en el PUT, y en el POST el brief pide el mismo código para "no existe" y para "bloqueado").
        var lote = await unitOfWork.Repository<LoteDiario>().FirstOrDefaultAsync(
            x => x.Id == datos.LoteDiarioId, cancellationToken: cancellationToken);
        if (lote is null || lote.Bloqueado)
        {
            return Fallo("diario.lote_bloqueado", "El lote no existe o está bloqueado.", "LoteDiarioId");
        }

        var plantilla = await unitOfWork.Repository<PlantillaDiario>().FirstOrDefaultAsync(
            x => x.Id == lote.PlantillaDiarioId, cancellationToken: cancellationToken);
        if (plantilla is null)
        {
            return Fallo("diario.lote_bloqueado", "El lote no existe o está bloqueado.", "LoteDiarioId");
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

        // 6. Cantidad > 0, hasta 6 decimales.
        if (datos.Cantidad <= 0 || decimal.Round(datos.Cantidad, 6) != datos.Cantidad)
        {
            return Fallo("diario.cantidad_invalida", "La cantidad debe ser mayor que cero y tener como máximo 6 decimales.", "Cantidad");
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
        var factorResultado = await conversion.ObtenerFactorAsync(datos.ProductoId, datos.UnidadMedidaId, cancellationToken);
        if (factorResultado.EsFallo)
        {
            var original = factorResultado.Errores[0];
            return Result<LineaDiarioCalculo>.Fallo(new Error(original.Codigo, original.Mensaje, "UnidadMedidaId"));
        }

        var factor = Math.Round(factorResultado.Valor, 6, MidpointRounding.AwayFromZero);

        // Defensivo: si ObtenerFactorAsync tuvo éxito, la unidad existe en el catálogo (es la base del producto, ya
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
        var importeCosto = datos.CostoUnitario is { } costo
            ? Math.Round(datos.Cantidad * factor * costo, 4, MidpointRounding.AwayFromZero)
            : 0m;

        return Result<LineaDiarioCalculo>.Exito(new LineaDiarioCalculo(
            factor, importeCosto, producto.Codigo, producto.Nombre, almacen.Codigo, almacenDestinoCodigo, unidadMedida.Codigo));
    }

    private static bool EsImporteValido(decimal valor) =>
        valor >= 0 && valor <= ImporteMaximo && decimal.Round(valor, 4) == valor;

    private static Result<LineaDiarioCalculo> Fallo(string codigo, string mensaje, string campo) =>
        Result<LineaDiarioCalculo>.Fallo(new Error(codigo, mensaje, campo));
}
