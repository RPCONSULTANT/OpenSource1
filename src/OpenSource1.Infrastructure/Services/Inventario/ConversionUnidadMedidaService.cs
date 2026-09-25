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
        var resolucion = await ResolverAsync(productoId, unidadMedidaId, cancellationToken);
        if (resolucion.EsFallo)
        {
            return Result<decimal>.Fallo(resolucion);
        }

        var (factor, decimalesBase) = resolucion.Valor;
        var convertido = Math.Round(cantidad * factor, decimalesBase, MidpointRounding.AwayFromZero);
        return Result<decimal>.Exito(convertido);
    }

    public async Task<Result<decimal>> ObtenerFactorAsync(Guid productoId, Guid unidadMedidaId, CancellationToken ct = default)
    {
        var resolucion = await ResolverAsync(productoId, unidadMedidaId, ct);
        return resolucion.EsFallo
            ? Result<decimal>.Fallo(resolucion)
            : Result<decimal>.Exito(resolucion.Valor.Factor);
    }

    /// <summary>
    /// Resuelve el factor (sin redondear) y los decimales de la unidad base. Única fuente del criterio de identidad y
    /// de los códigos de error, compartida por <see cref="ConvertirABaseAsync"/> y <see cref="ObtenerFactorAsync"/>.
    /// </summary>
    private async Task<Result<(decimal Factor, short DecimalesBase)>> ResolverAsync(
        Guid productoId, Guid unidadMedidaId, CancellationToken cancellationToken)
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
            return Result<(decimal, short)>.Fallo(new Error(
                "conversion.producto_no_encontrado", "No se encontró el producto solicitado.", "ProductoId"));
        }

        var decimalesBase = await context.UnidadesMedida
            .AsNoTracking()
            .Where(u => u.Id == unidadBaseId)
            .Select(u => (short?)u.Decimales)
            .FirstOrDefaultAsync(cancellationToken);

        if (decimalesBase is null)
        {
            return Result<(decimal, short)>.Fallo(new Error(
                "conversion.unidad_base_no_encontrada",
                "La unidad base del producto no existe en el catálogo de unidades de medida.", "ProductoId"));
        }

        // Identidad: la unidad base convierte con factor 1 SIN consultar UnidadesMedidaProducto (la fila de la base no se
        // almacena; si existiera una con otro factor, gana la identidad).
        if (unidadMedidaId == unidadBaseId.Value)
        {
            return Result<(decimal, short)>.Exito((1m, decimalesBase.Value));
        }

        var factorAsociado = await context.UnidadesMedidaProducto
            .AsNoTracking()
            .Where(x => x.ProductoId == productoId && x.UnidadMedidaId == unidadMedidaId)
            .Select(x => (decimal?)x.CantidadPorUnidadMedida)
            .FirstOrDefaultAsync(cancellationToken);

        if (factorAsociado is null)
        {
            return Result<(decimal, short)>.Fallo(new Error(
                "conversion.unidad_no_asociada",
                "La unidad de medida indicada no está asociada al producto.", "UnidadMedidaId"));
        }

        return Result<(decimal, short)>.Exito((factorAsociado.Value, decimalesBase.Value));
    }
}
