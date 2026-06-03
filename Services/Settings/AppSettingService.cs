using Microsoft.EntityFrameworkCore;
using test.Data.Entities;
using test.Data.UnitOfWork;

namespace test.Services.Settings;

public sealed class AppSettingService(IUnitOfWork unitOfWork) : IAppSettingService
{
    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await unitOfWork.Repository<AppSetting>()
            .Query()
            .Where(setting => setting.Key == key)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetValueAsync(
        string key,
        string value,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var repository = unitOfWork.Repository<AppSetting>();

        var setting = await repository
            .Query(asTracking: true)
            .FirstOrDefaultAsync(item => item.Key == key, cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Key = key,
                Value = value,
                Description = description
            };

            await repository.AddAsync(setting, cancellationToken);
        }
        else
        {
            setting.Value = value;
            setting.Description = description;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
            repository.Update(setting);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
