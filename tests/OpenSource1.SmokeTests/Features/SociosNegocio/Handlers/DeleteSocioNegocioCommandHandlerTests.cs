using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.SociosNegocio;
using OpenSource1.Application.Features.SociosNegocio.Commands;
using OpenSource1.Application.Features.SociosNegocio.Handlers;
using OpenSource1.Core.Entities;

namespace OpenSource1.SmokeTests.Features.SociosNegocio.Handlers;

public class DeleteSocioNegocioCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsNoEncontrado_WhenSocioNegocioDoesNotExist()
    {
        var repo = new Mock<IGenericRepository<SocioNegocio>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SocioNegocio?)null);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(repo.Object);
        var uso = new Mock<ISocioNegocioUsoService>();

        var handler = new DeleteSocioNegocioCommandHandler(unitOfWork.Object, uso.Object);
        var result = await handler.Handle(new DeleteSocioNegocioCommand(Guid.NewGuid()), default);

        Assert.True(result.EsFallo);
        Assert.Equal("socio_negocio.no_encontrado", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BloqueaAntesDeEvaluarElUso_BorraYConfirma_WhenSinUso()
    {
        var entity = new SocioNegocio { Codigo = "000001", NombreComercial = "Test" };
        var repo = new Mock<IGenericRepository<SocioNegocio>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(repo.Object);
        var orden = new List<string>();
        var uso = new Mock<ISocioNegocioUsoService>();
        uso.Setup(s => s.BloquearAsync(entity.Id, It.IsAny<CancellationToken>())).Callback(() => orden.Add("bloquear")).Returns(Task.CompletedTask);
        uso.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).Callback(() => orden.Add("uso")).ReturnsAsync(false);

        var handler = new DeleteSocioNegocioCommandHandler(unitOfWork.Object, uso.Object);
        var result = await handler.Handle(new DeleteSocioNegocioCommand(entity.Id), default);

        Assert.True(result.EsExito);
        Assert.Equal(["bloquear", "uso"], orden);
        repo.Verify(r => r.Remove(entity), Times.Once);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReturnsConflicto_WhenSocioEstaEnUso()
    {
        var entity = new SocioNegocio { Codigo = "000001", NombreComercial = "Test" };
        var repo = new Mock<IGenericRepository<SocioNegocio>>();
        repo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<SocioNegocio, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entity);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<SocioNegocio>()).Returns(repo.Object);
        var uso = new Mock<ISocioNegocioUsoService>();
        uso.Setup(s => s.EstaEnUsoAsync(entity.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new DeleteSocioNegocioCommandHandler(unitOfWork.Object, uso.Object);
        var result = await handler.Handle(new DeleteSocioNegocioCommand(entity.Id), default);

        Assert.True(result.EsFallo);
        Assert.Equal("socio_negocio.conflicto", result.Errores[0].Codigo);
        repo.Verify(r => r.Remove(It.IsAny<SocioNegocio>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
