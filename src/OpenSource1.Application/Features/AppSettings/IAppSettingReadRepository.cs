using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.AppSettings;

public interface IAppSettingReadRepository
{
    Task<AppSettingResponse?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task<Result<PagedResult<AppSettingResponse>>> ListAsync(
        PageRequest paginacion, CancellationToken cancellationToken = default);
}
