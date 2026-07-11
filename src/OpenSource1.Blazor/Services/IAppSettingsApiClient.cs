using OpenSource1.Application.Features.AppSettings.Dtos;
using System;

namespace OpenSource1.Blazor.Services;

[Obsolete("Modulo de prueba obsoleto. No usar AppSettings para nuevos desarrollos.")]
public interface IAppSettingsApiClient
{
    Task<IReadOnlyList<AppSettingResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> CreateAsync(AppSettingInput input, CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> UpdateAsync(AppSettingInput input, CancellationToken cancellationToken = default);
    Task<AppSettingOperationResult> DeleteAsync(string key, CancellationToken cancellationToken = default);
}

[Obsolete("Modulo de prueba obsoleto. No usar AppSettings para nuevos desarrollos.")]
public sealed record AppSettingInput(string? Key, string? Value, string? Description);

[Obsolete("Modulo de prueba obsoleto. No usar AppSettings para nuevos desarrollos.")]
public sealed record AppSettingOperationResult(bool Succeeded, string Message);
