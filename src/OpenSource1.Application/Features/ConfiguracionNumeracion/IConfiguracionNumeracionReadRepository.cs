using OpenSource1.Application.Features.ConfiguracionNumeracion.Dtos;

namespace OpenSource1.Application.Features.ConfiguracionNumeracion;

public interface IConfiguracionNumeracionReadRepository
{
    /// <summary>Las filas de la configuración (una por tipo) ordenadas por tipo, con los datos de su serie.</summary>
    Task<IReadOnlyList<ConfiguracionNumeracionResponse>> ListAsync(CancellationToken cancellationToken = default);
}
