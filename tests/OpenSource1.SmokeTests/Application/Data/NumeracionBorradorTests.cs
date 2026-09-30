using Moq;
using OpenSource1.Application.Data;
using OpenSource1.Core.Common;
using OpenSource1.Core.Enums;

namespace OpenSource1.SmokeTests.Application.Data;

/// <summary>
/// Helper de alta de borradores con series (ruling F10): usa las series elegidas o las configuradas, valida la de registro antes de
/// numerar y devuelve los errores del motor en el campo de la serie afectada.
/// </summary>
public class NumeracionBorradorTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 30);
    private static readonly Guid Borrador = Guid.NewGuid();
    private static readonly Guid Registro = Guid.NewGuid();

    [Fact]
    public async Task SinSeries_UsaLasConfiguradas_ValidaLaDeRegistro_YNumeraConLaDeBorrador()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.SerieConfiguradaAsync(TipoDocumentoSerie.BorradorFacturaVenta, default)).ReturnsAsync(Result<Guid>.Exito(Borrador));
        motor.Setup(m => m.SerieConfiguradaAsync(TipoDocumentoSerie.FacturaVenta, default)).ReturnsAsync(Result<Guid>.Exito(Registro));
        motor.Setup(m => m.ValidarSerieAsync(Registro, TipoDocumentoSerie.FacturaVenta, default)).ReturnsAsync(Result.Exito());
        motor.Setup(m => m.SiguienteAsync(Borrador, TipoDocumentoSerie.BorradorFacturaVenta, Hoy, default))
            .ReturnsAsync(Result<NumeroGenerado>.Exito(new NumeroGenerado("FV-BORR-9", null)));

        var resultado = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorFacturaVenta, TipoDocumentoSerie.FacturaVenta, null, null, Hoy);

        Assert.Equal(new SeriesBorradorNumeradas(Borrador, Registro, new NumeroGenerado("FV-BORR-9", null)), resultado.Valor);
    }

    [Fact]
    public async Task ConSeriesElegidas_NoLeeLaConfiguracion()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.ValidarSerieAsync(Registro, TipoDocumentoSerie.NotaCreditoVenta, default)).ReturnsAsync(Result.Exito());
        motor.Setup(m => m.SiguienteAsync(Borrador, TipoDocumentoSerie.BorradorNotaCreditoVenta, Hoy, default))
            .ReturnsAsync(Result<NumeroGenerado>.Exito(new NumeroGenerado("T1-000001", null)));

        var resultado = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorNotaCreditoVenta, TipoDocumentoSerie.NotaCreditoVenta, Borrador, Registro, Hoy);

        Assert.Equal((Borrador, Registro, "T1-000001"), (resultado.Valor.SerieBorradorId, resultado.Valor.SerieRegistroId, resultado.Valor.Numero.Numero));
    }

    [Fact]
    public async Task SerieRegistroInvalida_ErrorEnSerieRegistroId_YNoNumera()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.ValidarSerieAsync(Registro, TipoDocumentoSerie.FacturaVenta, default))
            .ReturnsAsync(Result.Fallo(new Error("numeracion.serie_inactiva", "Inactiva.", "SerieId")));

        var resultado = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorFacturaVenta, TipoDocumentoSerie.FacturaVenta, Borrador, Registro, Hoy);

        Assert.Equal(("numeracion.serie_inactiva", "SerieRegistroId"), (resultado.Errores[0].Codigo, resultado.Errores[0].Campo));
        motor.Verify(m => m.SiguienteAsync(It.IsAny<Guid>(), It.IsAny<TipoDocumentoSerie>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SerieBorradorInvalida_ErrorEnSerieBorradorId_YSinConfiguracion_TalCual()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.ValidarSerieAsync(Registro, TipoDocumentoSerie.FacturaVenta, default)).ReturnsAsync(Result.Exito());
        motor.Setup(m => m.SiguienteAsync(Borrador, TipoDocumentoSerie.BorradorFacturaVenta, Hoy, default))
            .ReturnsAsync(Result<NumeroGenerado>.Fallo(new Error("numeracion.tipo_incorrecto", "Otro tipo.", "SerieId")));
        motor.Setup(m => m.SerieConfiguradaAsync(TipoDocumentoSerie.FacturaVenta, default))
            .ReturnsAsync(Result<Guid>.Fallo(new Error("numeracion.sin_configuracion", "Sin serie.", "TipoDocumento")));

        var borradorInvalido = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorFacturaVenta, TipoDocumentoSerie.FacturaVenta, Borrador, Registro, Hoy);
        var sinConfiguracion = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorFacturaVenta, TipoDocumentoSerie.FacturaVenta, Borrador, null, Hoy);

        Assert.Equal(("numeracion.tipo_incorrecto", "SerieBorradorId"), (borradorInvalido.Errores[0].Codigo, borradorInvalido.Errores[0].Campo));
        Assert.Equal(("numeracion.sin_configuracion", "TipoDocumento"), (sinConfiguracion.Errores[0].Codigo, sinConfiguracion.Errores[0].Campo));
    }

    [Fact]
    public async Task SinConfiguracionDelTipoDeBorrador_TalCual_YNoValidaNiNumera()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.SerieConfiguradaAsync(TipoDocumentoSerie.BorradorNotaCreditoVenta, default))
            .ReturnsAsync(Result<Guid>.Fallo(new Error("numeracion.sin_configuracion", "Sin serie.", "TipoDocumento")));

        var resultado = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorNotaCreditoVenta, TipoDocumentoSerie.NotaCreditoVenta, null, Registro, Hoy);

        Assert.Equal(("numeracion.sin_configuracion", "TipoDocumento"), (Assert.Single(resultado.Errores).Codigo, resultado.Errores[0].Campo));
        motor.Verify(m => m.ValidarSerieAsync(It.IsAny<Guid>(), It.IsAny<TipoDocumentoSerie>(), It.IsAny<CancellationToken>()), Times.Never);
        motor.Verify(m => m.SiguienteAsync(It.IsAny<Guid>(), It.IsAny<TipoDocumentoSerie>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ErrorSinCampo_SinTransaccion_PasaTalCual()
    {
        var motor = new Mock<IGeneradorNumeroDocumento>(MockBehavior.Strict);
        motor.Setup(m => m.ValidarSerieAsync(Registro, TipoDocumentoSerie.FacturaVenta, default)).ReturnsAsync(Result.Exito());
        motor.Setup(m => m.SiguienteAsync(Borrador, TipoDocumentoSerie.BorradorFacturaVenta, Hoy, default))
            .ReturnsAsync(Result<NumeroGenerado>.Fallo(new Error("numeracion.sin_transaccion", "Sin transacción.")));

        var resultado = await motor.Object.NumerarBorradorAsync(
            TipoDocumentoSerie.BorradorFacturaVenta, TipoDocumentoSerie.FacturaVenta, Borrador, Registro, Hoy);

        Assert.Equal(new Error("numeracion.sin_transaccion", "Sin transacción."), Assert.Single(resultado.Errores));
    }
}
