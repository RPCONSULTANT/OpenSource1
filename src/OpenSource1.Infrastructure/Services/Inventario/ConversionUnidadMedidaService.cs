using Microsoft.EntityFrameworkCore;
using OpenSource1.Application.Services.Inventario;
using OpenSource1.Core.Common;
using OpenSource1.Infrastructure.Data;

namespace OpenSource1.Infrastructure.Services.Inventario;

public sealed class ConversionUnidadMedidaService(ApplicationDbContext context) : IConversionUnidadMedidaService
{
    public async Task<Result<decimal>> ConvertirABaseAsync(
        Guid productoId, Guid unidadMedidaId, decimal cantidad, CancellationToken cancellationToken = default)
    {
        var factor = await context.UnidadesMedidaProducto
            .AsNoTracking()
            .Where(x => x.ProductoId == productoId && x.UnidadMedidaId == unidadMedidaId)
            .Select(x => (decimal?)x.CantidadPorUnidadMedida)
            .FirstOrDefaultAsync(cancellationToken);

        if (factor is null)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.unidad_no_asociada",
                "La unidad de medida indicada no está asociada al producto.", "UnidadMedidaId"));
        }

        // La unidad base es la que el propio producto declara (Producto.UnidadMedidaBaseId); se resuelve contra el catálogo para
        // obtener sus Decimales. El redondeo usa SIEMPRE los decimales de la unidad base (el resultado está expresado en ella),
        // nunca los de la unidad de entrada.
        var unidadBaseId = await context.Productos
            .AsNoTracking()
            .Where(p => p.Id == productoId)
            .Select(p => (Guid?)p.UnidadMedidaBaseId)
            .FirstOrDefaultAsync(cancellationToken);

        if (unidadBaseId is null)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.producto_no_encontrado", "No se encontró el producto solicitado.", "ProductoId"));
        }

        var decimalesBase = await context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.Id == unidadBaseId)
            .Select(u => (short?)u.Decimales)
            .FirstOrDefaultAsync(cancellationToken);

        if (decimalesBase is null)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.unidad_base_no_encontrada",
                "La unidad base del producto no existe en el catálogo de unidades de medida.", "ProductoId"));
        }

        var convertido = Math.Round(cantidad * factor.Value, decimalesBase.Value, MidpointRounding.AwayFromZero);
        return Result<decimal>.Exito(convertido);
    }
}
