using Moq;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.DiariosInventario.Lotes;
using OpenSource1.Application.Features.DiariosInventario.Registros;
using OpenSource1.Application.Features.DiariosInventario.Registros.Commands;
using OpenSource1.Application.Features.DiariosInventario.Registros.Handlers;
using OpenSource1.Application.Services.Inventario;

namespace OpenSource1.SmokeTests.Features.DiariosInventario.Registros.Handlers;

public class PostearLoteDiarioCommandHandlerTests
{
    [Fact]
    public async Task Handle_InvocadoDentroDeOtraTransaccion_Lanza()
    {
        // Ronda de corrección final de la Fase 4 (punto 2): PostearLoteDiario garantiza su propia atomicidad y NO
        // puede ejecutarse dentro de una transacción ya abierta (BeginTransactionAsync se uniría a un ámbito anidado
        // que no hace nada al salir; ver el XML doc de PostearLoteDiarioCommand). Debe fallar alto y claro, antes de
        // tocar ninguna dependencia.
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.SetupGet(u => u.HayTransaccionActiva).Returns(true);

        var handler = new PostearLoteDiarioCommandHandler(
            unitOfWork.Object,
            Mock.Of<ILoteDiarioBloqueoService>(),
            Mock.Of<IRegistroLoteDiarioDatos>(),
            Mock.Of<IConversionUnidadMedidaService>(),
            Mock.Of<IRegistroMovimientosInventario>(),
            Mock.Of<IGeneradorNumeroDocumento>(),
            Mock.Of<IUsuarioActual>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new PostearLoteDiarioCommand(Guid.NewGuid()), default));

        unitOfWork.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
