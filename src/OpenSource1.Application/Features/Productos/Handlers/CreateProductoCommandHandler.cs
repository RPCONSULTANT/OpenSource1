using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Productos.Commands;
using OpenSource1.Application.Features.Productos.Dtos;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;

namespace OpenSource1.Application.Features.Productos.Handlers;

public sealed class CreateProductoCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateProductoCommand, Result<ProductoResponse>>
{
    public const string CategoriaPorDefecto = "GENERAL";
    public const string UnidadPorDefecto = "UND";

    public async Task<Result<ProductoResponse>> Handle(CreateProductoCommand request, CancellationToken cancellationToken)
    {
        var errores = ProductoValidator.Validar(request);
        if (errores.Count > 0)
        {
            return Result<ProductoResponse>.Fallo([.. errores]);
        }

        var referencias = await ProductoReglas.ResolverReferenciasAsync(
            unitOfWork, request.CategoriaId, request.UnidadMedidaBaseId, cancellationToken);
        if (!referencias.TryObtenerValor(out var catalogo))
        {
            return Result<ProductoResponse>.Fallo(referencias);
        }

        var resolucionGrupos = await ProductoReglas.ResolverGruposAsync(
            unitOfWork, request.GrupoProductoId, request.GrupoIvaProductoId, request.GrupoInventarioId, actual: null, cancellationToken);
        if (!resolucionGrupos.TryObtenerValor(out var grupos))
        {
            return Result<ProductoResponse>.Fallo(resolucionGrupos);
        }

        var imagen = await ProductoReglas.AsegurarImagenNoAsignadaAsync(unitOfWork, request.ImagePath, null, cancellationToken);
        if (imagen is not null)
        {
            return Result<ProductoResponse>.Fallo(imagen.Value);
        }

        var entity = new Producto
        {
            Codigo = request.Codigo.Trim(),
            Nombre = request.Nombre.Trim(),
            PrecioVenta = request.PrecioVenta,
            UnidadMedidaBaseId = catalogo.Unidad.Id,
            CategoriaId = catalogo.Categoria.Id,
            MetodoCosteo = request.MetodoCosteo,
            CostoEstandar = request.CostoEstandar,
            Bloqueado = request.Bloqueado,
            // CostoUnitario = 0 y CostoAjustado = true: sin movimientos todavía, no hay nada que ajustar.
            CostoUnitario = 0m,
            CostoAjustado = true,
            ImagePath = request.ImagePath,
            GrupoProductoId = request.GrupoProductoId,
            GrupoIvaProductoId = request.GrupoIvaProductoId,
            GrupoInventarioId = request.GrupoInventarioId
        };
        await unitOfWork.Repository<Producto>().AddAsync(entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        // Un producto recién creado no tiene movimientos: su existencia es 0 sin necesidad de consultar el libro.
        return Result<ProductoResponse>.Exito(ToResponse(entity, catalogo.Categoria, catalogo.Unidad, existencia: 0m, grupos));
    }

    /// <summary>Respuesta del alta/modificación: los nombres de categoría y unidad salen del catálogo (cadena vacía si la fila ya no existe).</summary>
    public static ProductoResponse ToResponse(
        Producto x, CategoriaProducto? categoria, UnidadMedida? unidad, decimal existencia, ProductoGruposContables? grupos = null) => new()
    {
        Id = x.Id,
        Codigo = x.Codigo,
        Nombre = x.Nombre,
        PrecioVenta = x.PrecioVenta,
        Existencia = existencia,
        CategoriaId = x.CategoriaId,
        CategoriaCodigo = categoria?.Codigo ?? string.Empty,
        CategoriaNombre = categoria?.Nombre ?? string.Empty,
        UnidadMedidaBaseId = x.UnidadMedidaBaseId,
        UnidadMedidaCodigo = unidad?.Codigo ?? string.Empty,
        UnidadMedidaNombre = unidad?.Nombre ?? string.Empty,
        UnidadMedidaBaseDecimales = unidad?.Decimales ?? 0,
        MetodoCosteo = x.MetodoCosteo,
        CostoUnitario = x.CostoUnitario,
        CostoEstandar = x.CostoEstandar,
        CostoAjustado = x.CostoAjustado,
        Bloqueado = x.Bloqueado,
        ImagePath = x.ImagePath,
        GrupoProductoId = x.GrupoProductoId,
        GrupoProductoCodigo = grupos?.Producto?.Codigo,
        GrupoIvaProductoId = x.GrupoIvaProductoId,
        GrupoIvaProductoCodigo = grupos?.IvaProducto?.Codigo,
        GrupoInventarioId = x.GrupoInventarioId,
        GrupoInventarioCodigo = grupos?.Inventario?.Codigo,
        CreatedAtUtc = x.CreatedAtUtc.UtcDateTime,
        UpdatedAtUtc = x.UpdatedAtUtc?.UtcDateTime,
        CreatedBy = x.CreatedBy,
        UpdatedBy = x.UpdatedBy
    };
}

/// <summary>Grupos contables resueltos del producto (null = sin grupo, o grupo colgante que no cambió).</summary>
public sealed record ProductoGruposContables(GrupoProducto? Producto, GrupoIvaProducto? IvaProducto, GrupoInventario? Inventario);

/// <summary>Reglas compartidas por Create/Update que tocan la base de datos.</summary>
internal static class ProductoReglas
{
    public sealed record Catalogo(CategoriaProducto Categoria, UnidadMedida Unidad);

    /// <summary>
    /// Resuelve los tres grupos contables (Task 5.3). Un grupo que se ASIGNA o CAMBIA (distinto del de <paramref name="actual"/>)
    /// debe existir y no estar borrado: si no, 400 <c>producto.grupo_invalido</c> en su campo (nunca <c>.no_encontrado</c>: es un
    /// dato del cuerpo). Uno que no cambia no se revalida (un producto con un grupo ya colgante sigue siendo editable, igual que
    /// categoría/unidad); se devuelve null para él si ya no existe.
    /// </summary>
    public static async Task<Result<ProductoGruposContables>> ResolverGruposAsync(
        IUnitOfWork unitOfWork, Guid? grupoProductoId, Guid? grupoIvaProductoId, Guid? grupoInventarioId, Producto? actual,
        CancellationToken cancellationToken)
    {
        var producto = await BuscarGrupoAsync<GrupoProducto>(unitOfWork, grupoProductoId, cancellationToken);
        if (producto is null && grupoProductoId is not null && grupoProductoId != actual?.GrupoProductoId)
        {
            return Result<ProductoGruposContables>.Fallo(ErrorGrupo("El grupo de producto indicado no existe.", nameof(Producto.GrupoProductoId)));
        }

        var ivaProducto = await BuscarGrupoAsync<GrupoIvaProducto>(unitOfWork, grupoIvaProductoId, cancellationToken);
        if (ivaProducto is null && grupoIvaProductoId is not null && grupoIvaProductoId != actual?.GrupoIvaProductoId)
        {
            return Result<ProductoGruposContables>.Fallo(ErrorGrupo("El grupo de IVA de producto indicado no existe.", nameof(Producto.GrupoIvaProductoId)));
        }

        var inventario = await BuscarGrupoAsync<GrupoInventario>(unitOfWork, grupoInventarioId, cancellationToken);
        if (inventario is null && grupoInventarioId is not null && grupoInventarioId != actual?.GrupoInventarioId)
        {
            return Result<ProductoGruposContables>.Fallo(ErrorGrupo("El grupo de inventario indicado no existe.", nameof(Producto.GrupoInventarioId)));
        }

        return Result<ProductoGruposContables>.Exito(new ProductoGruposContables(producto, ivaProducto, inventario));
    }

    private static Task<TGrupo?> BuscarGrupoAsync<TGrupo>(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken)
        where TGrupo : GrupoContable =>
        id is { } grupoId
            ? unitOfWork.Repository<TGrupo>().FirstOrDefaultAsync(x => x.Id == grupoId, cancellationToken: cancellationToken)
            : Task.FromResult<TGrupo?>(null);

    private static Error ErrorGrupo(string mensaje, string campo) => new("producto.grupo_invalido", mensaje, campo);

    /// <summary>
    /// Resuelve la categoría y la unidad base del ALTA: la que viene en el comando o, si no viene, <c>GENERAL</c>/<c>UND</c>. Devuelve
    /// un error con el campo del DTO si no existen o están borradas. Los códigos NO terminan en ".no_encontrado" a propósito: es un
    /// dato inválido del cuerpo (400), no un recurso de la URL inexistente (404).
    /// </summary>
    public static async Task<Result<Catalogo>> ResolverReferenciasAsync(
        IUnitOfWork unitOfWork, Guid? categoriaId, Guid? unidadId, CancellationToken cancellationToken)
    {
        var categoria = await BuscarCategoriaAsync(unitOfWork, categoriaId, cancellationToken);
        if (categoria is null)
        {
            return Result<Catalogo>.Fallo(ErrorCategoria(categoriaId));
        }

        var unidad = await BuscarUnidadAsync(unitOfWork, unidadId, cancellationToken);
        if (unidad is null)
        {
            return Result<Catalogo>.Fallo(ErrorUnidad(unidadId));
        }

        return Result<Catalogo>.Exito(new Catalogo(categoria, unidad));
    }

    // FirstOrDefaultAsync (consulta) y no GetByIdAsync (Find): solo la consulta aplica el filtro global de soft delete, y una
    // categoría o unidad borrada no es una referencia válida.
    public static Task<CategoriaProducto?> BuscarCategoriaAsync(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken) =>
        id is { } categoriaId
            ? unitOfWork.Repository<CategoriaProducto>().FirstOrDefaultAsync(x => x.Id == categoriaId, cancellationToken: cancellationToken)
            : unitOfWork.Repository<CategoriaProducto>().FirstOrDefaultAsync(x => x.Codigo == CreateProductoCommandHandler.CategoriaPorDefecto, cancellationToken: cancellationToken);

    public static Task<UnidadMedida?> BuscarUnidadAsync(IUnitOfWork unitOfWork, Guid? id, CancellationToken cancellationToken) =>
        id is { } unidadId
            ? unitOfWork.Repository<UnidadMedida>().FirstOrDefaultAsync(x => x.Id == unidadId, cancellationToken: cancellationToken)
            : unitOfWork.Repository<UnidadMedida>().FirstOrDefaultAsync(x => x.Codigo == CreateProductoCommandHandler.UnidadPorDefecto, cancellationToken: cancellationToken);

    public static Error ErrorCategoria(Guid? id) => new(
        "producto.categoria_invalida",
        id is null
            ? $"No existe la categoría por defecto {CreateProductoCommandHandler.CategoriaPorDefecto}: indique una categoría."
            : "La categoría indicada no existe.",
        "CategoriaId");

    public static Error ErrorUnidad(Guid? id) => new(
        "producto.unidad_medida_invalida",
        id is null
            ? $"No existe la unidad de medida por defecto {CreateProductoCommandHandler.UnidadPorDefecto}: indique una unidad base."
            : "La unidad de medida base indicada no existe.",
        "UnidadMedidaBaseId");

    /// <summary>
    /// Una imagen solo puede pertenecer a UN producto (si no, sustituir la de uno borraría la de otro).
    /// LIMITACIÓN CONOCIDA: es una consulta previa SIN índice único; dos peticiones concurrentes con el mismo <c>imagePath</c> pueden
    /// asignarlo a dos registros (medido: 40 de 40 rondas). Consecuencia: al sustituir la imagen de uno se borra el fichero y el otro queda
    /// con la imagen rota; NO es explotable para borrar el fichero de una víctima ajena. Corrección de raíz recomendada (no implementada):
    /// índice único parcial sobre <c>ImagePath</c> (no nulo y no borrado) por tabla, o comprobar referencias antes de borrar el fichero.
    /// </summary>
    public static async Task<Error?> AsegurarImagenNoAsignadaAsync(
        IUnitOfWork unitOfWork, string? imagePath, Guid? idActual, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return null;
        }

        var enUso = await unitOfWork.Repository<Producto>().FirstOrDefaultAsync(
            x => x.ImagePath == imagePath && (idActual == null || x.Id != idActual),
            cancellationToken: cancellationToken);

        return enUso is null
            ? null
            : new Error("producto.imagen_en_uso", "La imagen ya está asignada a otro producto.", OpenSource1.Application.Storage.RutaImagen.Campo);
    }
}
