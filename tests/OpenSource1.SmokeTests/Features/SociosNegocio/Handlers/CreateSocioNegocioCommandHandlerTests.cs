using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Common;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Contabilidad;
using OpenSource1.SmokeTests.TestInfrastructure;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class CreateSocioNegocioCommandHandlerTests
{
    private sealed class Contexto
    {
        public Mock<IGenericRepository<SocioNegocio>> Socios { get; } = new();
        public Mock<IGenericRepository<TerminoPago>> Terminos { get; } = new();
        public Mock<IUnitOfWork> UnitOfWork { get; } = new();
        public Mock<IGeneradorNumeroDocumento> Generador { get; } = new();
        public Mock<IAsyncDisposable> Transaccion { get; } = new();
        public SocioNegocio? Agregado { get; private set; }

        public Contexto(string numero = "000007")
        {
            Socios.Setup(r => r.AddAsync(It.IsAny<SocioNegocio>(), It.IsAny<CancellationToken>()))
                .Callback<SocioNegocio, CancellationToken>((e, _) => Agregado = e)
                .Returns(Task.CompletedTask);
            Socios.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SocioNegocio?)null);
            Terminos.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TerminoPago, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((TerminoPago?)null);

            UnitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(Socios.Object);
            UnitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(Terminos.Object);
            // Grupos contables (Task 5.3): el alta busca las semillas por defecto; sin filas, el socio queda sin grupos.
            UnitOfWork.Setup(u => u.Repository<GrupoNegocio>()).Returns(new RepositorioEnMemoria<GrupoNegocio>().Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoIvaNegocio>()).Returns(new RepositorioEnMemoria<GrupoIvaNegocio>().Repo);
            UnitOfWork.Setup(u => u.Repository<GrupoClienteContable>()).Returns(new RepositorioEnMemoria<GrupoClienteContable>().Repo);
            UnitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Transaccion.Object);

            Generador.Setup(g => g.SiguientePorTipoAsync(TipoDocumentoSerie.Cliente, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Result<NumeroGenerado>.Exito(new NumeroGenerado(numero, null)));
        }

        public CreateSocioNegocioCommandHandler Handler() => new(UnitOfWork.Object, Generador.Object);
    }

    [Fact]
    public async Task Handle_AsignaElCodigoDeLaSerieSocios_NormalizaYConfirmaLaTransaccion()
    {
        var ctx = new Contexto("000007");

        var result = await ctx.Handler().Handle(
            SocioNegocioTestData.Create(
                nombreComercial: "  Comercial SRL  ", tipo: TipoSocioNegocio.Ambos,
                tipoDocumento: TipoDocumentoFiscal.Rnc, numeroDocumento: " 101-00000-1 ",
                email: "  JP@MAIL.COM ", limiteCredito: 1500.5m, bloqueado: BloqueoSocioNegocio.Facturacion,
                direccionLinea1: " Calle 1 "),
            default);

        Assert.True(result.EsExito);
        Assert.NotNull(ctx.Agregado);
        Assert.Equal("000007", ctx.Agregado!.Codigo);
        Assert.Equal("Comercial SRL", ctx.Agregado.NombreComercial);
        Assert.Equal(TipoSocioNegocio.Ambos, ctx.Agregado.Tipo);
        Assert.Equal(TipoDocumentoFiscal.Rnc, ctx.Agregado.TipoDocumentoFiscal);
        Assert.Equal("101-00000-1", ctx.Agregado.NumeroDocumentoFiscal);
        Assert.Equal("JP@MAIL.COM", ctx.Agregado.Email);
        Assert.Equal(1500.5m, ctx.Agregado.LimiteCredito);
        Assert.Equal(BloqueoSocioNegocio.Facturacion, ctx.Agregado.Bloqueado);
        Assert.Equal("Calle 1", ctx.Agregado.Direccion?.Linea1);
        Assert.Equal("000007", result.Valor.Codigo);
        Assert.Equal(ctx.Agregado.Id, result.Valor.Id);

        ctx.UnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
        ctx.Generador.Verify(g => g.SiguientePorTipoAsync(TipoDocumentoSerie.Cliente, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DocumentoFiscal_SeGuardaEnMayusculas()
    {
        var ctx = new Contexto();

        var result = await ctx.Handler().Handle(
            SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.Cedula, numeroDocumento: " abc123 "), default);

        Assert.True(result.EsExito);
        Assert.Equal("ABC123", ctx.Agregado!.NumeroDocumentoFiscal);
    }

    [Fact]
    public async Task Handle_EmailYOpcionalesEnBlanco_SeGuardanComoNulos()
    {
        var ctx = new Contexto();

        var result = await ctx.Handler().Handle(SocioNegocioTestData.Create(email: "   "), default);

        Assert.True(result.EsExito);
        Assert.Null(ctx.Agregado!.Email);
        Assert.Null(ctx.Agregado.NumeroDocumentoFiscal);
        Assert.Null(ctx.Agregado.Direccion);
    }

    public static TheoryData<CreateSocioNegocioCommand, string> ComandosInvalidos => new()
    {
        { SocioNegocioTestData.Create(nombreComercial: ""), "NombreComercial" },
        { SocioNegocioTestData.Create(nombreComercial: new string('x', 201)), "NombreComercial" },
        { SocioNegocioTestData.Create(tipo: (TipoSocioNegocio)99), "Tipo" },
        { SocioNegocioTestData.Create(tipoDocumento: (TipoDocumentoFiscal)5), "TipoDocumentoFiscal" },
        { SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.Rnc, numeroDocumento: null), "NumeroDocumentoFiscal" },
        { SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.Cedula, numeroDocumento: "  "), "NumeroDocumentoFiscal" },
        { SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.Pasaporte, numeroDocumento: new string('9', 21)), "NumeroDocumentoFiscal" },
        { SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.SinDocumento, numeroDocumento: "123"), "NumeroDocumentoFiscal" },
        { SocioNegocioTestData.Create(limiteCredito: -0.01m), "LimiteCredito" },
        { SocioNegocioTestData.Create(limiteCredito: 0.00005m), "LimiteCredito" },
        { SocioNegocioTestData.Create(bloqueado: (BloqueoSocioNegocio)7), "Bloqueado" },
        { SocioNegocioTestData.Create(email: "no-es-un-correo"), "Email" },
        { SocioNegocioTestData.Create(paisCodigo: "ZZ"), "PaisCodigo" },
        { SocioNegocioTestData.Create(direccionLinea1: null, direccionLinea2: "solo linea 2"), "DireccionLinea1" },
    };

    [Theory]
    [MemberData(nameof(ComandosInvalidos))]
    public async Task Handle_DatosInvalidos_DevuelveFalloPorCampoSinTocarLaNumeracion(CreateSocioNegocioCommand comando, string campo)
    {
        var ctx = new Contexto();

        var result = await ctx.Handler().Handle(comando, default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == campo);
        ctx.UnitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        ctx.Generador.Verify(g => g.SiguientePorTipoAsync(TipoDocumentoSerie.Cliente, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TerminoPagoInexistente_FallaDespuesDeReservarSinConfirmar()
    {
        var ctx = new Contexto();
        var terminoId = Guid.NewGuid();

        var result = await ctx.Handler().Handle(SocioNegocioTestData.Create(terminoPagoId: terminoId), default);

        Assert.True(result.EsFallo);
        var error = Assert.Single(result.Errores);
        Assert.Equal("TerminoPagoId", error.Campo);
        Assert.Equal("socio_negocio.termino_pago_invalido", error.Codigo);

        // El número se reservó dentro de la transacción, pero no hubo commit: al salir del
        // await using la transacción se descarta (rollback) y el contador no se consume.
        ctx.Generador.Verify(g => g.SiguientePorTipoAsync(TipoDocumentoSerie.Cliente, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Once);
        ctx.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        ctx.Socios.Verify(r => r.AddAsync(It.IsAny<SocioNegocio>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.Transaccion.Verify(t => t.DisposeAsync(), Times.Once);
    }

    [Fact]
    public async Task Handle_TerminoPagoExistente_LoAsignaAlSocio()
    {
        var ctx = new Contexto();
        var termino = new TerminoPago { Codigo = "30D", Descripcion = "30 días" };
        ctx.Terminos.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TerminoPago, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(termino);

        var result = await ctx.Handler().Handle(SocioNegocioTestData.Create(terminoPagoId: termino.Id), default);

        Assert.True(result.EsExito);
        Assert.Equal(termino.Id, ctx.Agregado!.TerminoPagoId);
        Assert.Equal(termino.Id, result.Valor.TerminoPagoId);
    }

    [Fact]
    public async Task Handle_DocumentoFiscalDuplicado_DevuelveConflictoSinConfirmar()
    {
        var ctx = new Contexto();
        ctx.Socios.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SocioNegocio { Codigo = "000001", NombreComercial = "Otro" });

        var result = await ctx.Handler().Handle(
            SocioNegocioTestData.Create(tipoDocumento: TipoDocumentoFiscal.Rnc, numeroDocumento: "123"), default);

        Assert.True(result.EsFallo);
        var error = Assert.Single(result.Errores);
        Assert.Equal("socio_negocio.conflicto", error.Codigo);
        Assert.Equal("NumeroDocumentoFiscal", error.Campo);
        ctx.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SiLaNumeracionFalla_PropagaElErrorSinInsertar()
    {
        var ctx = new Contexto();
        ctx.Generador.Setup(g => g.SiguientePorTipoAsync(TipoDocumentoSerie.Cliente, It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<NumeroGenerado>.Fallo(new Error("numeracion.serie_agotada", "Agotada.")));

        var result = await ctx.Handler().Handle(SocioNegocioTestData.Create(), default);

        Assert.True(result.EsFallo);
        Assert.Equal("numeracion.serie_agotada", result.Errores[0].Codigo);
        ctx.Socios.Verify(r => r.AddAsync(It.IsAny<SocioNegocio>(), It.IsAny<CancellationToken>()), Times.Never);
        ctx.UnitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
