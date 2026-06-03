using test.Services.Auth;
using test.Services.Settings;

namespace test.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
