using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Application.Features.AppSettings.Queries;

namespace OpenSource1.Application.Features.AppSettings.Handlers;

public sealed class GetAppSettingByKeyQueryHandler(IAppSettingReadRepository readRepository)
    : IRequestHandler<GetAppSettingByKeyQuery, AppSettingResponse?>
{
    public Task<AppSettingResponse?> Handle(GetAppSettingByKeyQuery request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Key);
        return readRepository.GetByKeyAsync(request.Key, cancellationToken);
    }
}
