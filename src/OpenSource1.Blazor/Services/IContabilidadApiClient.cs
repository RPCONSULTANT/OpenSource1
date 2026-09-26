using OpenSource1.Application.Services.Contabilidad;

namespace OpenSource1.Blazor.Services;

/// <summary>Operaciones de la página de Contabilidad (Task 5.6) contra <c>api/contabilidad</c>.</summary>
public interface IContabilidadApiClient
{
    /// <summary><c>POST api/contabilidad/postear-costo-inventario</c> (CanModify): ejecuta el batch de costo de inventario.</summary>
    Task<PosteoCostoOperationResult> PostearCostoInventarioAsync(CancellationToken cancellationToken = default);
}

/// <summary>Resultado de ejecutar el batch: el resumen de la API si tuvo éxito; si no, el mensaje real de la API.</summary>
public sealed record PosteoCostoOperationResult(
    bool Succeeded, string Message, ResultadoPosteoCostoInventario? Valor = null, IReadOnlyList<string>? Errors = null);
