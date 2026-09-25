using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.Productos.Handlers;

/// <summary>Unidad de trabajo simulada con productos, categorías y unidades en memoria (con el catálogo por defecto GENERAL / UND).</summary>
internal sealed class ProductosFake
{
    public ProductosFake()
    {
        General = Categorias.Agregar(new CategoriaProducto { Codigo = "GENERAL", Nombre = "General" });
        Unidad = Unidades.Agregar(new UnidadMedida { Codigo = "UND", Nombre = "Unidad", Decimales = 0 });

        UnitOfWork = new Mock<IUnitOfWork>();
        UnitOfWork.Setup(u => u.Repository<Producto>()).Returns(Productos.Repo);
        UnitOfWork.Setup(u => u.Repository<CategoriaProducto>()).Returns(Categorias.Repo);
        UnitOfWork.Setup(u => u.Repository<UnidadMedida>()).Returns(Unidades.Repo);
        UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // MovimientoProducto no hereda de BaseEntity (Id es long, sin soft delete): no cabe en
        // RepositorioEnMemoria<T> (asume Id Guid + IsDeleted), así que se mockea aparte a mano.
        MovimientosRepo = new Mock<IGenericRepository<MovimientoProducto>>();
        MovimientosRepo
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<MovimientoProducto, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Expression<Func<MovimientoProducto, bool>> predicado, bool _, CancellationToken _) =>
                Movimientos.FirstOrDefault(predicado.Compile()));
        UnitOfWork.Setup(u => u.Repository<MovimientoProducto>()).Returns(MovimientosRepo.Object);
    }

    public Mock<IUnitOfWork> UnitOfWork { get; }
    public RepositorioEnMemoria<Producto> Productos { get; } = new();
    public RepositorioEnMemoria<CategoriaProducto> Categorias { get; } = new();
    public RepositorioEnMemoria<UnidadMedida> Unidades { get; } = new();
    public Mock<IGenericRepository<MovimientoProducto>> MovimientosRepo { get; }
    public List<MovimientoProducto> Movimientos { get; } = [];

    public CategoriaProducto General { get; }
    public UnidadMedida Unidad { get; }

    public CategoriaProducto AgregarCategoria(string codigo, string nombre) => Categorias.Agregar(new CategoriaProducto { Codigo = codigo, Nombre = nombre });

    public UnidadMedida AgregarUnidad(string codigo, string nombre) => Unidades.Agregar(new UnidadMedida { Codigo = codigo, Nombre = nombre, Decimales = 0 });

    /// <summary>Da de alta un movimiento mínimo del producto (para las pruebas de la guarda de cambio de unidad base).</summary>
    public void AgregarMovimiento(Guid productoId) => Movimientos.Add(new MovimientoProducto
    {
        ProductoId = productoId,
        AlmacenId = Guid.NewGuid(),
        ClaveOrigen = "TEST",
        CreatedBy = "test",
    });
}
