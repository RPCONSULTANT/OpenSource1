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
}
