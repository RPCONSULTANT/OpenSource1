namespace OpenSource1.Application.Features.AppSettings.Dtos;

/// <summary>Represents an application setting.</summary>
public sealed record AppSettingResponse(
    Guid Id,
    string Key,
    string Value,
    string? Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
