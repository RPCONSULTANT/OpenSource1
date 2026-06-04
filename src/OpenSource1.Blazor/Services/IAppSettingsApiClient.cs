using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Blazor.Services;

public interface IAppSettingsApiClient
{
    Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> CreateAsync(AppSettingInput input, CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> UpdateAsync(AppSettingInput input, CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record AppSettingInput(string? Key, string? Value, string? Description);

public sealed record AppSettingOperationResult(bool Succeeded, string Message);
