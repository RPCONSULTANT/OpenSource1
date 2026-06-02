using test.Services.Settings;

namespace test.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAppSettingService, AppSettingService>();

        return services;
    }
}
