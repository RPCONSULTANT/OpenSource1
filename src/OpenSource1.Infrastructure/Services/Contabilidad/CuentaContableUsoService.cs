using OpenSource1.Application.Services.Contabilidad;

namespace OpenSource1.Infrastructure.Services.Contabilidad;

/// <summary>
/// Implementación de la Task 5.2: ninguna tabla referencia todavía a <c>CuentasContables</c> (los
/// setups contables, los grupos de cliente y el libro contable nacen en las Tasks 5.3-5.5), así que
/// siempre responde sin uso. Cada task siguiente añade su propia consulta (Dapper, contra la sesión
/// del scope) antes de devolver <see langword="false"/>.
/// </summary>
public sealed class CuentaContableUsoService : ICuentaContableUsoService
{
    public Task<bool> EstaEnUsoAsync(Guid cuentaContableId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
