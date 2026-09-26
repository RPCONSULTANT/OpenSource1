using Moq;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

/// <summary>Clasificación contable del socio (Task 5.3): grupos opcionales en el alta y "null = conservar" en la modificación.</summary>
public sealed class SocioNegocioGruposContablesTests
{
    private sealed class Fake
    {
        public Fake()
        {
            UnitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(Socios.Repo);
            UnitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(Terminos.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoNegocio>()).Returns(Negocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaNegocio>()).Returns(IvaNegocio.Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoClienteContable>()).Returns(ClienteContable.Repo);
            UnitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IAsyncDisposable>().Object);
            Generador.Setup(g => g.SiguienteAsync("SOCIOS", It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<string>.Exito("000001"));

            Nacional = Negocio.Agregar(new GrupoNegocio { Codigo = "NACIONAL", Descripcion = "Nacional" });
            Itbis = IvaNegocio.Agregar(new GrupoIvaNegocio { Codigo = "ITBIS18", Descripcion = "ITBIS" });
            General = ClienteContable.Agregar(new GrupoClienteContable { Codigo = "GENERAL", Descripcion = "General", CuentaCxCId = Guid.NewGuid() });
        }

        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IGeneradorNumeroDocumento> Generador { get; } = new();
        public RepositorioEnMemoria<SocioNegocio> Socios { get; } = new();
        public RepositorioEnMemoria<TerminoPago> Terminos { get; } = new();
        public RepositorioEnMemoria<GrupoNegocio> Negocio { get; } = new();
        public RepositorioEnMemoria<GrupoIvaNegocio> IvaNegocio { get; } = new();
        public RepositorioEnMemoria<GrupoClienteContable> ClienteContable { get; } = new();
        public GrupoNegocio Nacional { get; }
        public GrupoIvaNegocio Itbis { get; }
        public GrupoClienteContable General { get; }

        public CreateSocioNegocioCommandHandler Crear() => new(UnitOfWork.Object, Generador.Object);
        public UpdateSocioNegocioCommandHandler Modificar() => new(UnitOfWork.Object);

        public SocioNegocio Existente() => Socios.Agregar(new SocioNegocio
        {
            Codigo = "000009", NombreComercial = "Socio", GrupoNegocioId = Nacional.Id, GrupoIvaNegocioId = Itbis.Id, GrupoClienteContableId = General.Id
        });
    }

    private static CreateSocioNegocioCommand Alta(Guid? negocio, Guid? iva, Guid? cliente) => new(
        "Socio", null, TipoSocioNegocio.Cliente, TipoDocumentoFiscal.SinDocumento, null, null, null, null, null, null, null, null, null,
        0m, BloqueoSocioNegocio.Ninguno, null, negocio, iva, cliente);

    [Fact]
    public async Task Create_ConGrupos_LosAsignaYDevuelveSusCodigos()
    {
        var fake = new Fake();

        var result = await fake.Crear().Handle(Alta(fake.Nacional.Id, fake.Itbis.Id, fake.General.Id), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        var guardado = Assert.Single(fake.Socios.Datos);
        Assert.Equal(fake.Nacional.Id, guardado.GrupoNegocioId);
        Assert.Equal(fake.Itbis.Id, guardado.GrupoIvaNegocioId);
        Assert.Equal(fake.General.Id, guardado.GrupoClienteContableId);
        Assert.Equal("NACIONAL", result.Valor.GrupoNegocioCodigo);
        Assert.Equal("ITBIS18", result.Valor.GrupoIvaNegocioCodigo);
        Assert.Equal("GENERAL", result.Valor.GrupoClienteContableCodigo);
    }

    [Theory]
    [InlineData("GrupoNegocioId")]
    [InlineData("GrupoIvaNegocioId")]
    [InlineData("GrupoClienteContableId")]
    public async Task Create_GrupoInexistente_Devuelve400GrupoInvalido_YNoGuarda(string campo)
    {
        var fake = new Fake();
        var inexistente = Guid.NewGuid();

        var result = await fake.Crear().Handle(Alta(
            campo == "GrupoNegocioId" ? inexistente : fake.Nacional.Id,
            campo == "GrupoIvaNegocioId" ? inexistente : fake.Itbis.Id,
            campo == "GrupoClienteContableId" ? inexistente : fake.General.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("socio_negocio.grupo_invalido", result.Errores[0].Codigo);
        Assert.Equal(campo, result.Errores[0].Campo);
        Assert.Empty(fake.Socios.Datos);
        fake.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_SinGrupos_ConservaLosGuardados()
    {
        var fake = new Fake();
        var socio = fake.Existente();

        var result = await fake.Modificar().Handle(SocioNegocioTestData.Update(socio.Id, nombreComercial: "Renombrado"), default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(fake.Nacional.Id, socio.GrupoNegocioId);
        Assert.Equal(fake.Itbis.Id, socio.GrupoIvaNegocioId);
        Assert.Equal(fake.General.Id, socio.GrupoClienteContableId);
        Assert.Equal("GENERAL", result.Valor.GrupoClienteContableCodigo);
    }

    [Fact]
    public async Task Update_CambiaElGrupoDeNegocio_ConservaLosDemas()
    {
        var fake = new Fake();
        var socio = fake.Existente();
        var exterior = fake.Negocio.Agregar(new GrupoNegocio { Codigo = "EXTERIOR", Descripcion = "Exterior" });

        var result = await fake.Modificar().Handle(
            SocioNegocioTestData.Update(socio.Id) with { GrupoNegocioId = exterior.Id }, default);

        Assert.True(result.EsExito, result.EsFallo ? result.Errores[0].Mensaje : null);
        Assert.Equal(exterior.Id, socio.GrupoNegocioId);
        Assert.Equal(fake.General.Id, socio.GrupoClienteContableId);
        Assert.Equal("EXTERIOR", result.Valor.GrupoNegocioCodigo);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Update_GrupoInexistenteOGuidVacio_Devuelve400YNoModifica(bool guidVacio)
    {
        var fake = new Fake();
        var socio = fake.Existente();

        var result = await fake.Modificar().Handle(
            SocioNegocioTestData.Update(socio.Id, nombreComercial: "Otro") with { GrupoClienteContableId = guidVacio ? Guid.Empty : Guid.NewGuid() },
            default);

        Assert.True(result.EsFallo);
        Assert.Equal("socio_negocio.grupo_invalido", result.Errores[0].Codigo);
        Assert.Equal("GrupoClienteContableId", result.Errores[0].Campo);
        Assert.Equal(fake.General.Id, socio.GrupoClienteContableId);
        Assert.Equal("Socio", socio.NombreComercial);
        fake.UnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
