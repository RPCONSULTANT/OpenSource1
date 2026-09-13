using MediatR;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Core.Common;

namespace OpenSource1.Application.Features.AppSettings.Queries;

public sealed record ListAppSettingsQuery(PageRequest Paginacion) : IRequest<Result<PagedResult<AppSettingResponse>>>;
