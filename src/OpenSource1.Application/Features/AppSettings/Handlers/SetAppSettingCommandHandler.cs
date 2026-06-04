using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.AppSettings.Commands;
using OpenSource1.Application.Features.AppSettings.Dtos;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.AppSettings.Handlers;

public sealed class SetAppSettingCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<SetAppSettingCommand, AppSettingResponse>
{
    public async Task<AppSettingResponse> Handle(SetAppSettingCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Key);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Value);

        var repository = unitOfWork.Repository<AppSetting>();
        var setting = await repository.FirstOrDefaultAsync(
            item => item.Key == request.Key,
            asTracking: true,
            cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Key = request.Key,
                Value = request.Value,
                Description = request.Description
            };

            await repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = request.Value;
            setting.Description = request.Description;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
            repository.Update(setting);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AppSettingResponse
        {
            Id          = setting.Id,
            Key         = setting.Key,
            Value       = setting.Value,
            Description = setting.Description,
            CreatedAtUtc = setting.CreatedAtUtc.UtcDateTime,
            UpdatedAtUtc = setting.UpdatedAtUtc?.UtcDateTime
        };
    }
}
