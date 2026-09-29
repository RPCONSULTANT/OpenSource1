using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposContables.Commands;
using OpenSource1.Application.Features.GruposContables.Handlers;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.GruposContables.Handlers;

/// <summary>Handlers genéricos de los cinco grupos contables simples (Task 5.3), con un repositorio en memoria por tabla.</summary>
public sealed class GrupoContableHandlersTests
{
    private sealed class Fake
    {
        public Fake()
        {
            UnitOfWork.Setup(u => u.Repository<GrupoNegocio>()).Returns(Negocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoProducto>()).Returns(Producto.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaNegocio>()).Returns(IvaNegocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaProducto>()).Returns(IvaProducto.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoInventario>()).Returns(Inventario.Repo);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        }

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IGrupoContableUsoService> Uso { get; } = new();
        public RepositorioEnMemoria<GrupoNegocio> Negocio { get; } = new();
        public RepositorioEnMemoria<GrupoProducto> Producto { get; } = new();
        public RepositorioEnMemoria<GrupoIvaNegocio> IvaNegocio { get; } = new();
        public RepositorioEnMemoria<GrupoIvaProducto> IvaProducto { get; } = new();
        public RepositorioEnMemoria<GrupoInventario> Inventario { get; } = new();

        public IReadOnlyList<GrupoContable> Datos(TipoGrupoContable tipo) => tipo switch
        {
            TipoGrupoContable.Negocio => Negocio.Datos,
            TipoGrupoContable.Producto => Producto.Datos,
            TipoGrupoContable.IvaNegocio => IvaNegocio.Datos,
            TipoGrupoContable.IvaProducto => IvaProducto.Datos,
            TipoGrupoContable.Inventario => Inventario.Datos,
            _ => throw new ArgumentOutOfRangeException(nameof(tipo))
        };
    }

    [Theory]
    [InlineData(TipoGrupoContable.Negocio)]
    [InlineData(TipoGrupoContable.Producto)]
    [InlineData(TipoGrupoContable.IvaNegocio)]
    [InlineData(TipoGrupoContable.IvaProducto)]
    [InlineData(TipoGrupoContable.Inventario)]
    public async Task Create_GuardaEnLaTablaDeSuTipo_YNormalizaElCodigo(TipoGrupoContable tipo)
    {
        var fake = new Fake();

        var result = await new CreateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateGrupoContableCommand(tipo, " nuevo-1 ", " Descripción "), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(tipo, result.Valor.Tipo);
        Assert.Equal("NUEVO-1", result.Valor.Codigo);
        Assert.Equal("Descripción", result.Valor.Descripcion);
        var guardado = Assert.Single(fake.Datos(tipo));
        Assert.Equal(result.Valor.Id, guardado.Id);
        // Ninguna otra tabla recibió nada.
        Assert.Equal(1, Enum.GetValues<TipoGrupoContable>().Sum(t => fake.Datos(t).Count));
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", "Descripción", "grupo_contable.codigo_requerido", "Codigo")]
    [InlineData("CON ESPACIO", "Descripción", "grupo_contable.codigo_invalido", "Codigo")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", "Descripción", "grupo_contable.codigo_invalido", "Codigo")]
    [InlineData("VALIDO", " ", "grupo_contable.descripcion_requerida", "Descripcion")]
    public async Task Create_DatosInvalidos_Devuelve400ConElCampo(string codigo, string descripcion, string error, string campo)
    {
        var fake = new Fake();

        var result = await new CreateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateGrupoContableCommand(TipoGrupoContable.Producto, codigo, descripcion), default);

        Assert.True(result.EsFallo);
        Assert.Equal(error, result.Errores[0].Codigo);
        Assert.Equal(campo, result.Errores[0].Campo);
        Assert.Empty(fake.Producto.Datos);
    }

    [Fact]
    public async Task Create_TipoFueraDelEnum_DevuelveTipoInvalido()
    {
        var fake = new Fake();

        var result = await new CreateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new CreateGrupoContableCommand((TipoGrupoContable)99, "A", "B"), default);

        Assert.True(result.EsFallo);
        Assert.Equal("grupo_contable.tipo_invalido", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Update_NoExiste_DevuelveNoEncontrado()
    {
        var fake = new Fake();

        var result = await new UpdateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateGrupoContableCommand(TipoGrupoContable.Inventario, Guid.NewGuid(), "X", "Y", 1), default);

        Assert.True(result.EsFallo);
        Assert.Equal("grupo_contable.no_encontrado", result.Errores[0].Codigo);
    }

    [Fact]
    public async Task Update_BuscaSoloEnLaTablaDelTipo_AplicaYFijaLaVersionOriginal()
    {
        var fake = new Fake();
        var grupo = fake.IvaNegocio.Agregar(new GrupoIvaNegocio { Codigo = "ITBIS18", Descripcion = "Viejo" });

        // Mismo Id pedido con OTRO tipo: no se encuentra (cada tipo es su propia tabla).
        var otroTipo = await new UpdateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateGrupoContableCommand(TipoGrupoContable.IvaProducto, grupo.Id, "X", "Y", 7), default);
        Assert.Equal("grupo_contable.no_encontrado", otroTipo.Errores[0].Codigo);

        var result = await new UpdateGrupoContableCommandHandler(fake.UnitOfWork.Object)
            .Handle(new UpdateGrupoContableCommand(TipoGrupoContable.IvaNegocio, grupo.Id, " itbis16 ", " Nuevo ", 7), default);

        Assert.True(result.EsExito);
        Assert.Equal("ITBIS16", grupo.Codigo);
        Assert.Equal("Nuevo", grupo.Descripcion);
        fake.IvaNegocio.Mock.Verify(r => r.EstablecerVersionOriginal(grupo, 7), Times.Once);
        fake.IvaNegocio.Mock.Verify(r => r.Update(grupo), Times.Once);
    }

    [Fact]
    public async Task Delete_EnUso_Devuelve409YNoBorra()
    {
        var fake = new Fake();
        var grupo = fake.Producto.Agregar(new GrupoProducto { Codigo = "BIENES", Descripcion = "Bienes" });
        fake.Uso.Setup(u => u.EstaEnUsoAsync(TipoGrupoContable.Producto, grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await new DeleteGrupoContableCommandHandler(fake.UnitOfWork.Object, fake.Uso.Object)
            .Handle(new DeleteGrupoContableCommand(TipoGrupoContable.Producto, grupo.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("grupo_contable.conflicto", result.Errores[0].Codigo);
        fake.Producto.Mock.Verify(r => r.Remove(It.IsAny<GrupoProducto>()), Times.Never);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SinUso_BorraYGuarda()
    {
        var fake = new Fake();
        var grupo = fake.Negocio.Agregar(new GrupoNegocio { Codigo = "EXTERIOR", Descripcion = "Exterior" });

        var result = await new DeleteGrupoContableCommandHandler(fake.UnitOfWork.Object, fake.Uso.Object)
            .Handle(new DeleteGrupoContableCommand(TipoGrupoContable.Negocio, grupo.Id), default);

        Assert.True(result.EsExito);
        fake.Uso.Verify(u => u.EstaEnUsoAsync(TipoGrupoContable.Negocio, grupo.Id, It.IsAny<CancellationToken>()), Times.Once);
        fake.Negocio.Mock.Verify(r => r.Remove(grupo), Times.Once);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Delete_NoExiste_DevuelveNoEncontrado()
    {
        var fake = new Fake();

        var result = await new DeleteGrupoContableCommandHandler(fake.UnitOfWork.Object, fake.Uso.Object)
            .Handle(new DeleteGrupoContableCommand(TipoGrupoContable.Negocio, Guid.NewGuid()), default);

        Assert.Equal("grupo_contable.no_encontrado", result.Errores[0].Codigo);
    }
}
