using System.Linq.Expressions;
using Moq;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.ConfiguracionNumeracion;
using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;
using OpenSource1.Application.Features.Series;
using OpenSource1.Core.Entities;
using OpenSource1.Core.Enums;
using Configuracion = OpenSource1.Core.Entities.ConfiguracionNumeracion;

namespace OpenSource1.SmokeTests.Features.ConfiguracionNumeracion;

/// <summary>
/// PUT de configuración de numeración (S6): sin fila de configuración responde 404 sin bloquear la serie; con fila, la serie se bloquea
/// antes de escribir la configuración (orden serie -> configuración).
/// </summary>
public class UpdateConfiguracionNumeracionCommandHandlerTests
{
    private const TipoDocumentoSerie Tipo = TipoDocumentoSerie.Cobro;

    [Fact]
    public async Task SinFilaDeConfiguracion_404_SinBloquearLaSerie()
    {
        var (unitOfWork, configuraciones, _) = Configurar(fila: null);
        var series = new Mock<ISerieReadRepository>(MockBehavior.Strict);

        var resultado = await new UpdateConfiguracionNumeracionCommandHandler(
            unitOfWork.Object, Mock.Of<IConfiguracionNumeracionReadRepository>(), series.Object)
            .Handle(new UpdateConfiguracionNumeracionCommand(Tipo, Guid.NewGuid(), 1), default);

        Assert.Equal(("configuracion_numeracion.no_encontrado", "TipoDocumento"), (Assert.Single(resultado.Errores).Codigo, resultado.Errores[0].Campo));
        series.VerifyNoOtherCalls();
        configuraciones.Verify(r => r.Update(It.IsAny<Configuracion>()), Times.Never);
    }

    [Fact]
    public async Task ConFila_BloqueaLaSerieAntesDeEscribirLaConfiguracion()
    {
        var fila = new Configuracion { TipoDocumento = Tipo, SerieId = Guid.NewGuid() };
        var serie = new Serie { Codigo = "COBRO2", Descripcion = "Cobros 2", TipoDocumento = Tipo, Activa = true };
        var (unitOfWork, configuraciones, pasos) = Configurar(fila, serie);
        var series = new Mock<ISerieReadRepository>(MockBehavior.Strict);
        series.Setup(s => s.BloquearSerieAsync(serie.Id, It.IsAny<CancellationToken>()))
            .Callback(() => pasos.Add("bloquear serie")).ReturnsAsync(Tipo);
        var lectura = new Mock<IConfiguracionNumeracionReadRepository>();
        lectura.Setup(l => l.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ConfiguracionNumeracionResponse { TipoDocumento = Tipo, SerieId = serie.Id }]);

        var resultado = await new UpdateConfiguracionNumeracionCommandHandler(unitOfWork.Object, lectura.Object, series.Object)
            .Handle(new UpdateConfiguracionNumeracionCommand(Tipo, serie.Id, 7), default);

        Assert.Equal(serie.Id, resultado.Valor.SerieId);
        Assert.Equal(serie.Id, fila.SerieId);
        Assert.Equal(["bloquear serie", "actualizar configuración", "commit"], pasos);
        configuraciones.Verify(r => r.EstablecerVersionOriginal(fila, 7), Times.Once);
    }

    [Fact]
    public async Task SerieDeOtroTipo_400EnSerieId_SinEscribir()
    {
        var fila = new Configuracion { TipoDocumento = Tipo, SerieId = Guid.NewGuid() };
        var (unitOfWork, configuraciones, _) = Configurar(fila);
        var serieId = Guid.NewGuid();
        var series = new Mock<ISerieReadRepository>(MockBehavior.Strict);
        series.Setup(s => s.BloquearSerieAsync(serieId, It.IsAny<CancellationToken>())).ReturnsAsync(TipoDocumentoSerie.Cliente);

        var resultado = await new UpdateConfiguracionNumeracionCommandHandler(
            unitOfWork.Object, Mock.Of<IConfiguracionNumeracionReadRepository>(), series.Object)
            .Handle(new UpdateConfiguracionNumeracionCommand(Tipo, serieId, 1), default);

        Assert.Equal(("configuracion_numeracion.serie_invalida", "SerieId"), (Assert.Single(resultado.Errores).Codigo, resultado.Errores[0].Campo));
        configuraciones.Verify(r => r.Update(It.IsAny<Configuracion>()), Times.Never);
        unitOfWork.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (Mock<IUnitOfWork> UnitOfWork, Mock<IGenericRepository<Configuracion>> Configuraciones, List<string> Pasos)
        Configurar(Configuracion? fila, Serie? serie = null)
    {
        var pasos = new List<string>();
        var configuraciones = new Mock<IGenericRepository<Configuracion>>();
        configuraciones.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Configuracion, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(fila);
        configuraciones.Setup(r => r.Update(It.IsAny<Configuracion>())).Callback(() => pasos.Add("actualizar configuración"));
        var repositorioSeries = new Mock<IGenericRepository<Serie>>();
        repositorioSeries.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Serie, bool>>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(serie);
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());
        unitOfWork.Setup(u => u.Repository<Configuracion>()).Returns(configuraciones.Object);
        unitOfWork.Setup(u => u.Repository<Serie>()).Returns(repositorioSeries.Object);
        unitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => pasos.Add("commit")).Returns(Task.CompletedTask);
        return (unitOfWork, configuraciones, pasos);
    }
}
