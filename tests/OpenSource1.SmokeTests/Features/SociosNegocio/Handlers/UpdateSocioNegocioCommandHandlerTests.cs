using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class UpdateSocioNegocioCommandHandlerTests
{
    private static (UpdateSocioNegocioCommandHandler Handler, Mock<IGenericRepository<SocioNegocio>> Repo, Mock<IUnitOfWork> UnitOfWork) Crear(SocioNegocio? existente)
    {
        var repo = new Mock<IGenericRepository<SocioNegocio>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            // Evalúa el predicado de verdad: la búsqueda por Id encuentra al socio, pero la de
            // "otro socio con el mismo documento" (que excluye su propio Id) no.
            .ReturnsAsync((Expression<Func<SocioNegocio, bool>> predicado, bool _, CancellationToken _) =>
                existente is not null && predicado.Compile()(existente) ? existente : null);

        var terminos = new Mock<IGenericRepository<TerminoPago>>();
        terminos.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<TerminoPago, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TerminoPago?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(terminos.Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        return (new UpdateSocioNegocioCommandHandler(unitOfWork.Object), repo, unitOfWork);
    }

    [Fact]
    public async Task Handle_ReturnsNoEncontrado_WhenSocioNegocioDoesNotExist()
    {
        var (handler, _, unitOfWork) = Crear(null);

        var result = await handler.Handle(SocioNegocioTestData.Update(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("socio_negocio.no_encontrado", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActualizaLosCamposPeroNuncaElCodigo()
    {
        var entity = new SocioNegocio { Codigo = "000042", NombreComercial = "Antiguo" };
        var (handler, _, unitOfWork) = Crear(entity);

        var result = await handler.Handle(
            SocioNegocioTestData.Update(
                entity.Id, nombreComercial: " Nuevo ", tipo: TipoSocioNegocio.Proveedor,
                tipoDocumento: TipoDocumentoFiscal.Cedula, numeroDocumento: " 001-1 ", limiteCredito: 20m),
            default);

        Assert.True(result.EsExito);
        Assert.Equal("000042", entity.Codigo);
        Assert.Equal("000042", result.Valor.Codigo);
        Assert.Equal("Nuevo", entity.NombreComercial);
        Assert.Equal(TipoSocioNegocio.Proveedor, entity.Tipo);
        Assert.Equal("001-1", entity.NumeroDocumentoFiscal);
        Assert.Equal(20m, entity.LimiteCredito);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DatosInvalidos_DevuelveFalloSinConsultarNiGuardar()
    {
        var entity = new SocioNegocio { Codigo = "000001", NombreComercial = "Antiguo" };
        var (handler, _, unitOfWork) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, nombreComercial: " ", limiteCredito: -5m), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "NombreComercial");
        Assert.Contains(result.Errores, e => e.Campo == "LimiteCredito");
        Assert.Equal("Antiguo", entity.NombreComercial);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TerminoPagoInexistente_DevuelveFalloEnElCampo()
    {
        var entity = new SocioNegocio { Codigo = "000001", NombreComercial = "Antiguo" };
        var (handler, _, unitOfWork) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, terminoPagoId: Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("TerminoPagoId", result.Errores[0].Campo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SocioNegocio SocioCompleto(Guid? terminoPagoId = null) => new()
    {
        Codigo = "000010",
        NombreComercial = "Bloqueado SRL",
        RazonSocial = "Bloqueado Razon",
        Tipo = TipoSocioNegocio.Proveedor,
        TipoDocumentoFiscal = TipoDocumentoFiscal.Rnc,
        NumeroDocumentoFiscal = "RNC-1",
        Ciudad = "Santiago",
        TerminoPagoId = terminoPagoId,
        LimiteCredito = 900m,
        Bloqueado = BloqueoSocioNegocio.Todo
    };

    [Fact]
    public async Task Handle_ModificacionParcial_ConservaLosCamposNuevosNoInformados()
    {
        var termino = Guid.NewGuid();
        var entity = SocioCompleto(termino);
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, nombreComercial: "Renombrado"), default);

        Assert.True(result.EsExito, string.Join("; ", result.Errores.Select(e => e.Codigo)));
        Assert.Equal("Renombrado", entity.NombreComercial);
        Assert.Equal(TipoSocioNegocio.Proveedor, entity.Tipo);
        Assert.Equal(BloqueoSocioNegocio.Todo, entity.Bloqueado);
        Assert.Equal(900m, entity.LimiteCredito);
        Assert.Equal(TipoDocumentoFiscal.Rnc, entity.TipoDocumentoFiscal);
        Assert.Equal("RNC-1", entity.NumeroDocumentoFiscal);
        Assert.Equal("Bloqueado Razon", entity.RazonSocial);
        Assert.Equal("Santiago", entity.Ciudad);
        Assert.Equal(termino, entity.TerminoPagoId);
        Assert.Equal(BloqueoSocioNegocio.Todo, result.Valor.Bloqueado);
    }

    [Fact]
    public async Task Handle_ValoresInformados_SeAplican_IncluidoDesbloquearExplicitamente()
    {
        var entity = SocioCompleto();
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(
            SocioNegocioTestData.Update(entity.Id, tipo: TipoSocioNegocio.Cliente, bloqueado: BloqueoSocioNegocio.Ninguno, limiteCredito: 0m), default);

        Assert.True(result.EsExito);
        Assert.Equal(TipoSocioNegocio.Cliente, entity.Tipo);
        Assert.Equal(BloqueoSocioNegocio.Ninguno, entity.Bloqueado);
        Assert.Equal(0m, entity.LimiteCredito);
    }

    [Fact]
    public async Task Handle_CadenaVaciaLimpiaLosOpcionales_YGuidVacioLimpiaElTermino()
    {
        var entity = SocioCompleto(Guid.NewGuid());
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(
            SocioNegocioTestData.Update(
                entity.Id, tipoDocumento: TipoDocumentoFiscal.SinDocumento, numeroDocumento: "  ",
                razonSocial: "", ciudad: "", terminoPagoId: Guid.Empty), default);

        Assert.True(result.EsExito, string.Join("; ", result.Errores.Select(e => e.Codigo)));
        Assert.Null(entity.NumeroDocumentoFiscal);
        Assert.Null(entity.RazonSocial);
        Assert.Null(entity.Ciudad);
        Assert.Null(entity.TerminoPagoId);
        Assert.Equal(TipoDocumentoFiscal.SinDocumento, entity.TipoDocumentoFiscal);
    }

    [Fact]
    public async Task Handle_CambiarASinDocumentoSinLimpiarElNumero_DevuelveFallo()
    {
        var entity = SocioCompleto();
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, tipoDocumento: TipoDocumentoFiscal.SinDocumento), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "NumeroDocumentoFiscal");
        Assert.Equal(TipoDocumentoFiscal.Rnc, entity.TipoDocumentoFiscal);
    }

    [Fact]
    public async Task Handle_TerminoDePagoColgante_NoSeRevalidaSiNoCambia()
    {
        // La referencia apunta a un término que ya no existe (el repositorio de términos devuelve
        // null): el socio sigue siendo editable si no toca el término, ya sea omitiéndolo o reenviándolo.
        var colgante = Guid.NewGuid();
        var entity = SocioCompleto(colgante);
        var (handler, _, unitOfWork) = Crear(entity);

        var omitido = await handler.Handle(SocioNegocioTestData.Update(entity.Id, nombreComercial: "Solo nombre"), default);
        var reenviado = await handler.Handle(SocioNegocioTestData.Update(entity.Id, nombreComercial: "Reenviado", terminoPagoId: colgante), default);

        Assert.True(omitido.EsExito);
        Assert.True(reenviado.EsExito);
        Assert.Equal(colgante, entity.TerminoPagoId);
        unitOfWork.Verify(u => u.Repository<TerminoPago>(), Times.Never);
    }

    [Fact]
    public async Task Handle_CambiarElTerminoAUnoInexistente_SiSeValida()
    {
        var entity = SocioCompleto(Guid.NewGuid());
        var (handler, _, unitOfWork) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, terminoPagoId: Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("TerminoPagoId", result.Errores[0].Campo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DocumentoFiscal_SeNormalizaAMayusculasYSinEspacios()
    {
        var entity = SocioCompleto();
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(SocioNegocioTestData.Update(entity.Id, numeroDocumento: "  abc-123 "), default);

        Assert.True(result.EsExito);
        Assert.Equal("ABC-123", entity.NumeroDocumentoFiscal);
    }

    [Theory]
    [InlineData("0.00005")]
    [InlineData("10.12345")]
    public async Task Handle_LimiteCreditoConMasDeCuatroDecimales_DevuelveFallo(string valor)
    {
        var entity = SocioCompleto();
        var (handler, _, _) = Crear(entity);

        var result = await handler.Handle(
            SocioNegocioTestData.Update(entity.Id, limiteCredito: decimal.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Campo == "LimiteCredito");
        Assert.Equal(900m, entity.LimiteCredito);
    }
}
