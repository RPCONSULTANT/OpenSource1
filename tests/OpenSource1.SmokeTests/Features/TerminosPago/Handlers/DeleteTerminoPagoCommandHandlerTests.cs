using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.TerminosPago.Commands;
using OpenSource1.Application.Features.TerminosPago.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.TerminosPago.Handlers;

public class DeleteTerminoPagoCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFallo_WhenTerminoPagoDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync((TerminoPago?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);

        var handler = new DeleteTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteTerminoPagoCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Contains(result.Errores, e => e.Codigo == "termino_pago.no_encontrado");
    }

    [Fact]
    public async Task Handle_RemovesAndSaves_WhenTerminoPagoExists()
    {
        var entity = new TerminoPago { Codigo = "COD", Descripcion = "Test", DiasVencimiento = 0, DiasDescuento = 0, PorcentajeDescuento = 0m };
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(SociosQueUsan(null).Object);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteTerminoPagoCommand(entity.Id), default);

        Assert.True(result.EsExito);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenTerminoEstaAsignadoAUnSocio()
    {
        var entity = new TerminoPago { Codigo = "COD", Descripcion = "Test", DiasVencimiento = 0, DiasDescuento = 0, PorcentajeDescuento = 0m };
        var repo = new Mock<IGenericRepository<TerminoPago>>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<object[]>(), It.IsAny<CancellationToken>())).ReturnsAsync(entity);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<TerminoPago>()).Returns(repo.Object);
        unitOfWork.Setup(u => u.Repository<SocioNegocio>())
            .Returns(SociosQueUsan(new SocioNegocio { Codigo = "000001", NombreComercial = "Usa el termino" }).Object);

        var handler = new DeleteTerminoPagoCommandHandler(unitOfWork.Object);
        var result = await handler.Handle(new DeleteTerminoPagoCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("termino_pago.en_uso.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<TerminoPago>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IGenericRepository<SocioNegocio>> SociosQueUsan(SocioNegocio? socio)
    {
        var socios = new Mock<IGenericRepository<SocioNegocio>>();
        socios.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(socio);
        return socios;
    }
}
