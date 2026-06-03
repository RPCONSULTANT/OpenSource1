using MediatR;

namespace OpenSource1.Application.Features.AppSettings.Commands;

public sealed record DeleteAppSettingCommand(string Key) : IRequest<bool>;
