using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Core.Entities;
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
    }

    public Mock<IUnitOfWork> UnitOfWork { get; }
    public RepositorioEnMemoria<Producto> Productos { get; } = new();
    public RepositorioEnMemoria<CategoriaProducto> Categorias { get; } = new();
    public RepositorioEnMemoria<UnidadMedida> Unidades { get; } = new();

    public CategoriaProducto General { get; }
    public UnidadMedida Unidad { get; }

    public CategoriaProducto AgregarCategoria(string codigo, string nombre) => Categorias.Agregar(new CategoriaProducto { Codigo = codigo, Nombre = nombre });

    public UnidadMedida AgregarUnidad(string codigo, string nombre) => Unidades.Agregar(new UnidadMedida { Codigo = codigo, Nombre = nombre, Decimales = 0 });
}
