using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Application.Features.AppSettings.Queries;

public sealed record ListAppSettingsQuery : IRequest<IReadOnlyList<AppSettingResponse>>;
