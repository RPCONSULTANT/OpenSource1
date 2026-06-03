using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Application.Features.AppSettings;

public interface IAppSettingReadRepository
{
    Task<AppSettingResponse?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default);
}
