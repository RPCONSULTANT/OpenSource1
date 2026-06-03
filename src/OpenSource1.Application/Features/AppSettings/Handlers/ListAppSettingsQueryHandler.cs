using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Application.Features.AppSettings.Queries;

namespace OpenSource1.Application.Features.AppSettings.Handlers;

public sealed class ListAppSettingsQueryHandler(IAppSettingReadRepository readRepository)
    : IRequestHandler<ListAppSettingsQuery, IReadOnlyList<AppSettingResponse>>
{
    public Task<IReadOnlyList<AppSettingResponse>> Handle(ListAppSettingsQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(cancellationToken);
}
