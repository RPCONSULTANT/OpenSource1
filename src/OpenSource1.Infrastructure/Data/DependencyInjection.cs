using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Application.Features.Entradas;
using OpenSource1.Application.Features.Users;
using OpenSource1.Application.Services.Auth;
using OpenSource1.Application.Services.Settings;
using OpenSource1.Infrastructure.Data.Queries;
using OpenSource1.Infrastructure.Data.Repositories;
using OpenSource1.Infrastructure.Data.UnitOfWork;
using OpenSource1.Infrastructure.Identity;
using OpenSource1.Infrastructure.Services.Auth;
using OpenSource1.Infrastructure.Services.Settings;
using OpenSource1.Infrastructure.Services.Users;

namespace OpenSource1.Infrastructure.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationData(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));

        var applicationConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        var identityConnectionString = configuration.GetConnectionString("IdentityConnection")
            ?? throw new InvalidOperationException("Connection string 'IdentityConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(applicationConnectionString));

        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseNpgsql(identityConnectionString));

        services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();
        services.AddScoped<IAppSettingReadRepository, DapperAppSettingReadRepository>();
        services.AddScoped<IEntradaReadRepository, DapperEntradaReadRepository>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, OpenSource1.Infrastructure.Data.UnitOfWork.UnitOfWork>();
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
