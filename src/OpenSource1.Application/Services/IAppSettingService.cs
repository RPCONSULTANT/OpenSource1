namespace OpenSource1.Application.Services.Settings;

public interface IAppSettingService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);
}
