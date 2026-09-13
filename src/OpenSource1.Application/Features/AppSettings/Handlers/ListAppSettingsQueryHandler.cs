using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Application.Features.AppSettings.Queries;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.AppSettings.Handlers;

public sealed class ListAppSettingsQueryHandler(IAppSettingReadRepository readRepository)
    : IRequestHandler<ListAppSettingsQuery, Result<PagedResult<AppSettingResponse>>>
{
    public Task<Result<PagedResult<AppSettingResponse>>> Handle(ListAppSettingsQuery request, CancellationToken cancellationToken) =>
        readRepository.ListAsync(request.Paginacion, cancellationToken);
}
