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

        // Hasta la Task 2.9 (Producto.UnidadMedidaBaseId) la unidad base del producto es la que
        // el propio producto declara con su código legado; se resuelve contra el catálogo nuevo
        // por código para obtener sus Decimales. El redondeo usa SIEMPRE los decimales de la
        // unidad base (el resultado está expresado en ella), nunca los de la unidad de entrada.
        var codigoBase = await context.Productos
            .AsNoTracking()
            .Where(p => p.Id == productoId)
            .Select(p => p.UnidadMedida.Codigo)
            .FirstOrDefaultAsync(cancellationToken);

        if (codigoBase is null)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.producto_no_encontrado", "No se encontró el producto solicitado.", "ProductoId"));
        }

        var decimalesBase = await context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.Codigo == codigoBase)
            .Select(u => (short?)u.Decimales)
            .FirstOrDefaultAsync(cancellationToken);

        if (decimalesBase is null)
        {
            return Result<decimal>.Fallo(new Error(
                "conversion.unidad_base_no_encontrada",
                $"La unidad base '{codigoBase}' del producto no existe en el catálogo de unidades de medida.", "ProductoId"));
        }

        var convertido = Math.Round(cantidad * factor.Value, decimalesBase.Value, MidpointRounding.AwayFromZero);
        return Result<decimal>.Exito(convertido);
    }
}
