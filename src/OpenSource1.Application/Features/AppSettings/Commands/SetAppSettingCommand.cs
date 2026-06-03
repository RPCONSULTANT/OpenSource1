using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;

namespace OpenSource1.Application.Features.AppSettings.Commands;

public sealed record SetAppSettingCommand(string Key, string Value, string? Description = null) : IRequest<AppSettingResponse>;
