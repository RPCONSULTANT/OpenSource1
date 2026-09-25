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
        // La unidad base es la que el propio producto declara (Producto.UnidadMedidaBaseId); se resuelve contra el catálogo para
        // obtener sus Decimales. El redondeo usa SIEMPRE los decimales de la unidad base (el resultado está expresado en ella),
        // nunca los de la unidad de entrada. El filtro global de borrado lógico de Producto aplica en esta consulta.
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

        // Identidad: la unidad base convierte con factor 1 SIN consultar UnidadesMedidaProducto (la fila de la base no se
        // almacena; si existiera una con otro factor, gana la identidad).
        decimal factor;
        if (unidadMedidaId == unidadBaseId.Value)
        {
            factor = 1m;
        }
        else
        {
            var factorAsociado = await context.UnidadesMedidaProducto
                .AsNoTracking()
                .Where(x => x.ProductoId == productoId && x.UnidadMedidaId == unidadMedidaId)
                .Select(x => (decimal?)x.CantidadPorUnidadMedida)
                .FirstOrDefaultAsync(cancellationToken);

            if (factorAsociado is null)
            {
                return Result<decimal>.Fallo(new Error(
                    "conversion.unidad_no_asociada",
                    "La unidad de medida indicada no está asociada al producto.", "UnidadMedidaId"));
            }

            factor = factorAsociado.Value;
        }

        var convertido = Math.Round(cantidad * factor, decimalesBase.Value, MidpointRounding.AwayFromZero);
        return Result<decimal>.Exito(convertido);
    }
}
