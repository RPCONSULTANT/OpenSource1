using Microsoft.EntityFrameworkCore;
using test.Data;
using test.Data.Entities;

namespace test.Services.Settings;

public sealed class AppSettingService(ApplicationDbContext dbContext) : IAppSettingService
{
    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return await dbContext.AppSettings
            .AsNoTracking()
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

        var setting = await dbContext.AppSettings
            .FirstOrDefaultAsync(item => item.Key == key, cancellationToken);

        if (setting is null)
        {
            setting = new AppSetting
            {
                Key = key,
                Value = value,
                Description = description
            };

            dbContext.AppSettings.Add(setting);
        }
        else
        {
            setting.Value = value;
            setting.Description = description;
            setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
