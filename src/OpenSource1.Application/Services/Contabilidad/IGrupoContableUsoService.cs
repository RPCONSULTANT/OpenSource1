using OpenSource1.Core.Enums;

namespace OpenSource1.Application.Services.Contabilidad;

/// <summary>
/// Guarda de uso de los grupos contables (Task 5.3): borrar un grupo usado por un producto o un socio de negocio vivo (no
/// borrado lógicamente) se rechaza con 409 (<c>grupo_contable.conflicto</c> / <c>grupo_cliente_contable.conflicto</c>). La
/// Task 5.4 amplía la implementación con las referencias desde los setups contables, sin tocar a los llamadores
/// (<c>DeleteGrupoContableCommandHandler</c>, <c>DeleteGrupoClienteContableCommandHandler</c>), igual que
/// <see cref="ICuentaContableUsoService"/>.
/// </summary>
public interface IGrupoContableUsoService
{
    /// <summary>¿Algún producto (Producto/IvaProducto/Inventario) o socio (Negocio/IvaNegocio) vivo usa el grupo?</summary>
    Task<bool> EstaEnUsoAsync(TipoGrupoContable tipo, Guid grupoId, CancellationToken cancellationToken = default);

    /// <summary>¿Algún socio de negocio vivo usa el grupo contable de cliente?</summary>
    Task<bool> GrupoClienteContableEnUsoAsync(Guid grupoId, CancellationToken cancellationToken = default);
}
