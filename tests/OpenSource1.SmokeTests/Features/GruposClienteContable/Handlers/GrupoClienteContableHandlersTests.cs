using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.GruposClienteContable.Commands;
using OpenSource1.Application.Features.GruposClienteContable.Handlers;
using OpenSource1.Application.Services.Contabilidad;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.GruposClienteContable.Handlers;

public sealed class GrupoClienteContableHandlersTests
{
    private sealed class Fake
    {
        public Fake()
        {
            UnitOfWork.Setup(u => u.Repository<GrupoClienteContable>()).Returns(Grupos.Repo);
            UnitOfWork.Setup(u => u.Repository<CuentaContable>()).Returns(Cuentas.Repo);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            CxC = Cuenta("1201", TipoCuentaContable.Posteo);
            Descuento = Cuenta("4102", TipoCuentaContable.Posteo);
            Encabezado = Cuenta("1", TipoCuentaContable.Encabezado);
            Bloqueada = Cuenta("1299", TipoCuentaContable.Posteo, bloqueada: true);
        }

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IGrupoContableUsoService> Uso { get; } = new();
        public RepositorioEnMemoria<GrupoClienteContable> Grupos { get; } = new();
        public RepositorioEnMemoria<CuentaContable> Cuentas { get; } = new();
        public CuentaContable CxC { get; }
        public CuentaContable Descuento { get; }
        public CuentaContable Encabezado { get; }
        public CuentaContable Bloqueada { get; }

        public CuentaContable Cuenta(string numero, TipoCuentaContable tipo, bool bloqueada = false) => Cuentas.Agregar(new CuentaContable
        {
            Numero = numero, Nombre = $"Cuenta {numero}", TipoCuenta = tipo, TipoResultado = TipoResultadoCuenta.Balance, Bloqueada = bloqueada
        });

        public CreateGrupoClienteContableCommandHandler Crear() => new(UnitOfWork.Object);
        public UpdateGrupoClienteContableCommandHandler Modificar() => new(UnitOfWork.Object);
        public DeleteGrupoClienteContableCommandHandler Borrar() => new(UnitOfWork.Object, Uso.Object);
    }

    [Fact]
    public async Task Create_ConCuentasDePosteoNoBloqueadas_GuardaYRotulaLasCuentas()
    {
        var fake = new Fake();

        var result = await fake.Crear().Handle(
            new CreateGrupoClienteContableCommand(" mayoristas ", "Clientes mayoristas", fake.CxC.Id, fake.Descuento.Id), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal("MAYORISTAS", result.Valor.Codigo);
        Assert.Equal("1201", result.Valor.CuentaCxCNumero);
        Assert.Equal("4102", result.Valor.CuentaDescuentoNumero);
        Assert.Null(result.Valor.CuentaInteresId);
        var guardado = Assert.Single(fake.Grupos.Datos);
        Assert.Equal(fake.CxC.Id, guardado.CuentaCxCId);
    }

    [Theory]
    [InlineData("encabezado")]
    [InlineData("bloqueada")]
    [InlineData("inexistente")]
    public async Task Create_CuentaCxCNoValida_Devuelve400CuentaInvalida(string caso)
    {
        var fake = new Fake();
        var cuentaId = caso switch
        {
            "encabezado" => fake.Encabezado.Id,
            "bloqueada" => fake.Bloqueada.Id,
            _ => Guid.NewGuid()
        };

        var result = await fake.Crear().Handle(new CreateGrupoClienteContableCommand("G1", "Grupo", cuentaId), default);

        Assert.True(result.EsFallo);
        Assert.Equal("grupo_cliente_contable.cuenta_invalida", result.Errores[0].Codigo);
        Assert.Equal("CuentaCxCId", result.Errores[0].Campo);
        Assert.Empty(fake.Grupos.Datos);
    }

    [Fact]
    public async Task Create_CuentaDescuentoDeEncabezado_Devuelve400EnSuCampo()
    {
        var fake = new Fake();

        var result = await fake.Crear().Handle(
            new CreateGrupoClienteContableCommand("G1", "Grupo", fake.CxC.Id, CuentaDescuentoId: fake.Encabezado.Id), default);

        Assert.Equal("grupo_cliente_contable.cuenta_invalida", result.Errores[0].Codigo);
        Assert.Equal("CuentaDescuentoId", result.Errores[0].Campo);
    }

    [Fact]
    public async Task Update_DescuentoNullConserva_GuidVacioQuita_YFijaXmin()
    {
        var fake = new Fake();
        var interes = fake.Cuenta("4201", TipoCuentaContable.Posteo);
        var grupo = fake.Grupos.Agregar(new GrupoClienteContable
        {
            Codigo = "G1", Descripcion = "Grupo", CuentaCxCId = fake.CxC.Id, CuentaDescuentoId = fake.Descuento.Id, CuentaInteresId = interes.Id
        });

        var result = await fake.Modificar().Handle(
            new UpdateGrupoClienteContableCommand(grupo.Id, "G1", "Renombrado", fake.CxC.Id, null, Guid.Empty, 42), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal("Renombrado", grupo.Descripcion);
        Assert.Equal(fake.Descuento.Id, grupo.CuentaDescuentoId);
        Assert.Null(grupo.CuentaInteresId);
        fake.Grupos.Mock.Verify(r => r.EstablecerVersionOriginal(grupo, 42), Times.Once);
    }

    [Fact]
    public async Task Update_CuentaQueNoCambiaNoSeRevalida_PeroUnaNuevaBloqueadaSeRechaza()
    {
        var fake = new Fake();
        // El grupo apunta a una CxC que se bloqueó DESPUÉS: renombrar el grupo sigue siendo posible.
        var grupo = fake.Grupos.Agregar(new GrupoClienteContable { Codigo = "G1", Descripcion = "Grupo", CuentaCxCId = fake.Bloqueada.Id });

        var renombrar = await fake.Modificar().Handle(
            new UpdateGrupoClienteContableCommand(grupo.Id, "G1", "Otro nombre", fake.Bloqueada.Id, null, null, 1), default);
        Assert.True(renombrar.EsExito, renombrar.EsFallo ? renombrar.Errores[0].Mensaje : null);

        var cambiarAEncabezado = await fake.Modificar().Handle(
            new UpdateGrupoClienteContableCommand(grupo.Id, "G1", "Otro nombre", fake.Encabezado.Id, null, null, 1), default);
        Assert.Equal("grupo_cliente_contable.cuenta_invalida", cambiarAEncabezado.Errores[0].Codigo);
        Assert.Equal(fake.Bloqueada.Id, grupo.CuentaCxCId);
    }

    [Fact]
    public async Task Delete_EnUsoPorUnSocio_Devuelve409()
    {
        var fake = new Fake();
        var grupo = fake.Grupos.Agregar(new GrupoClienteContable { Codigo = "GENERAL", Descripcion = "General", CuentaCxCId = fake.CxC.Id });
        fake.Uso.Setup(u => u.GrupoClienteContableEnUsoAsync(grupo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await fake.Borrar().Handle(new DeleteGrupoClienteContableCommand(grupo.Id), default);

        Assert.Equal("grupo_cliente_contable.conflicto", result.Errores[0].Codigo);
        fake.Grupos.Mock.Verify(r => r.Remove(It.IsAny<GrupoClienteContable>()), Times.Never);
    }

    [Fact]
    public async Task Delete_SinUso_Borra_YNoExistente404()
    {
        var fake = new Fake();
        var grupo = fake.Grupos.Agregar(new GrupoClienteContable { Codigo = "G2", Descripcion = "G2", CuentaCxCId = fake.CxC.Id });

        Assert.True((await fake.Borrar().Handle(new DeleteGrupoClienteContableCommand(grupo.Id), default)).EsExito);
        fake.Grupos.Mock.Verify(r => r.Remove(grupo), Times.Once);

        var noExiste = await fake.Borrar().Handle(new DeleteGrupoClienteContableCommand(Guid.NewGuid()), default);
        Assert.Equal("grupo_cliente_contable.no_encontrado", noExiste.Errores[0].Codigo);
    }
}
