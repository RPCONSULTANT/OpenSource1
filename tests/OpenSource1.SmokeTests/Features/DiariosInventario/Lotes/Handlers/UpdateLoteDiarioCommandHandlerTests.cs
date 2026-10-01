using Moq;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Commands;
using OpenSource1.Application.Features.DiariosInventario.Lotes.Handlers;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Entities.Inventario;
using OpenSource1.Core.Enums;
using OpenSource1.SmokeTests.TestInfrastructure;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Lotes.Handlers;

public class UpdateLoteDiarioCommandHandlerTests
{
    [Fact]
    public async Task Handle_LoteInexistente_DevuelveFallo()
    {
        var unitOfWork = ArmarUnitOfWork(out _, out _, out _);

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        var result = await handler.Handle(
            new UpdateLoteDiarioCommand(Guid.NewGuid(), "COD", "Nombre", null, null, 1), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario_lote.no_encontrado", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActualizaCodigoYNombreYConservaSerieNoInformada()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);
        var serieOriginal = Guid.NewGuid();
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo", SerieId = serieOriginal });

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        var result = await handler.Handle(
            new UpdateLoteDiarioCommand(entity.Id, " new ", " Nuevo ", null, null, 1), default);

        Assert.True(result.EsExito);
        Assert.Equal("NEW", entity.Codigo);
        Assert.Equal("Nuevo", entity.Nombre);
        Assert.Equal(serieOriginal, entity.SerieId);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SerieEmpty_LimpiaLaSerie()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo", SerieId = Guid.NewGuid() });

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        var result = await handler.Handle(
            new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", Guid.Empty, null, 1), default);

        Assert.True(result.EsExito);
        Assert.Null(entity.SerieId);
    }

    [Fact]
    public async Task Handle_SerieNuevaInexistente_DevuelveFalloSinGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo" });

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        var result = await handler.Handle(
            new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", Guid.NewGuid(), null, 1), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.serie_invalida", result.Errores[0].Codigo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        lotes.Mock.Verify(r => r.Update(It.IsAny<LoteDiario>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SerieQueNoEsDeDiario_DevuelveSerieInvalidaSinGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out var series, out _);
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo" });
        var socios = series.Agregar(new Serie { Codigo = "SOCIOS", Descripcion = "Códigos de socios de negocio" });

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        var result = await handler.Handle(
            new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", socios.Id, null, 1), default);

        Assert.True(result.EsFallo);
        Assert.Equal("diario.serie_invalida", result.Errores[0].Codigo);
        Assert.Equal("SerieId", result.Errores[0].Campo);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
        lotes.Mock.Verify(r => r.Update(It.IsAny<LoteDiario>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EstableceLaVersionOriginalDeXminAntesDeGuardar()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out _, out _);
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo" });

        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));
        await handler.Handle(new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", null, null, 42), default);

        lotes.Mock.Verify(r => r.EstablecerVersionOriginal(entity, 42), Times.Once);
    }

    [Fact]
    public async Task Handle_SerieDeDiarioSinPrefijoDiario_SeAcepta_YLaInactivaNo()
    {
        var unitOfWork = ArmarUnitOfWork(out var lotes, out var series, out _);
        var entity = lotes.Agregar(new LoteDiario { PlantillaDiarioId = Guid.NewGuid(), Codigo = "OLD", Nombre = "Viejo" });
        var propia = series.Agregar(new Serie { Codigo = "AJUSTES", Descripcion = "Ajustes", TipoDocumento = TipoDocumentoSerie.DiarioInventario });
        var inactiva = series.Agregar(new Serie
        {
            Codigo = "DIARIO-VIEJA", Descripcion = "Vieja", TipoDocumento = TipoDocumentoSerie.DiarioInventario, Activa = false,
        });
        var handler = new UpdateLoteDiarioCommandHandler(unitOfWork.Object, LecturaSeries(unitOfWork));

        var aceptada = await handler.Handle(new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", propia.Id, null, 1), default);
        var rechazada = await handler.Handle(new UpdateLoteDiarioCommand(entity.Id, "OLD", "Viejo", inactiva.Id, null, 1), default);

        Assert.True(aceptada.EsExito, aceptada.EsFallo ? aceptada.Errores[0].Codigo : string.Empty);
        Assert.Equal(("diario.serie_invalida", "SerieId"), (rechazada.Errores[0].Codigo, rechazada.Errores[0].Campo));
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IUnitOfWork> ArmarUnitOfWork(
        out RepositorioEnMemoria<LoteDiario> lotes,
        out RepositorioEnMemoria<Serie> series,
        out RepositorioEnMemoria<LineaDiario> lineas)
    {
        lotes = new RepositorioEnMemoria<LoteDiario>();
        series = new RepositorioEnMemoria<Serie>();
        lineas = new RepositorioEnMemoria<LineaDiario>();

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.Repository<LoteDiario>()).Returns(lotes.Repo);
        unitOfWork.Setup(u => u.Repository<Serie>()).Returns(series.Repo);
        unitOfWork.Setup(u => u.Repository<LineaDiario>()).Returns(lineas.Repo);
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return unitOfWork;
    }

    /// <summary>La lectura <c>FOR SHARE</c> de la serie, simulada sobre el mismo repositorio en memoria de series.</summary>
    private static ISerieReadRepository LecturaSeries(Mock<IUnitOfWork> unitOfWork)
    {
        var lectura = new Mock<ISerieReadRepository>();
        lectura.Setup(r => r.LeerSerieCompartidaAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(async (Guid id, CancellationToken ct) =>
                await unitOfWork.Object.Repository<Serie>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken: ct) is { } s
                    ? new EstadoSerie(s.TipoDocumento, s.Activa)
                    : null);
        return lectura.Object;
    }
}
