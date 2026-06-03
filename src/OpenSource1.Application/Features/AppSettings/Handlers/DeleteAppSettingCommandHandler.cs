using MediatR;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.AppSettings.Commands;
using OpenSource1.Core.Entities;

namespace OpenSource1.Application.Features.AppSettings.Handlers;

public sealed class DeleteAppSettingCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAppSettingCommand, bool>
{
    public async Task<bool> Handle(DeleteAppSettingCommand request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Key);

        var repository = unitOfWork.Repository<AppSetting>();
        var setting = await repository.FirstOrDefaultAsync(
            item => item.Key == request.Key,
            asTracking: true,
            cancellationToken);

        if (setting is null)
        {
            return false;
        }

        repository.Remove(setting);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
