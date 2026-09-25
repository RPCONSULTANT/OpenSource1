using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Data;
using OpenSource1.Application.Data.Repositories;
using OpenSource1.Application.Data.UnitOfWork;
using OpenSource1.Application.Features.Clientes;
using OpenSource1.Application.Features.AppSettings;
using OpenSource1.Application.Features.Entradas;
using OpenSource1.Application.Features.Productos;
using OpenSource1.Application.Features.Users;
using OpenSource1.Application.Services.Auth;
using OpenSource1.Application.Services.Settings;
using OpenSource1.Infrastructure.Data.Repositories;
using OpenSource1.Infrastructure.Data.Queries;
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

        services.AddScoped<DbSession>();
        services.AddScoped<IDbSession>(sp => sp.GetRequiredService<DbSession>());

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseNpgsql(sp.GetRequiredService<DbSession>().Connection));

        // La base de Identity mantiene su propia conexión: es otra base de datos.
        // La cadena de conexión se lee vía IConfiguration resuelto de DI en la fábrica del
        // DbContext (no en una variable capturada aquí durante el registro): igual que
        // DbSession con "DefaultConnection", esto difiere la lectura hasta que el DbContext se
        // construye realmente, después de que WebApplicationFactory haya fusionado su
        // configuración de prueba. Leer "IdentityConnection" de forma eager en este método (como
        // hacía antes) capturaba siempre el valor real de appsettings.json, igual que el defecto
        // de validación eager de Jwt:SigningKey en Identity/DependencyInjection.cs.
        services.AddDbContext<AppIdentityDbContext>((sp, options) =>
        {
            var identityConnectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("IdentityConnection")
                ?? throw new InvalidOperationException("Connection string 'IdentityConnection' was not found.");

            options.UseNpgsql(identityConnectionString);
        });

        services.AddScoped<IAppSettingReadRepository, DapperAppSettingReadRepository>();
        services.AddScoped<IEntradaReadRepository, DapperEntradaReadRepository>();
        services.AddScoped<IClienteReadRepository, DapperClienteReadRepository>();
        services.AddScoped<IProductoReadRepository, DapperProductoReadRepository>();
        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, OpenSource1.Infrastructure.Data.UnitOfWork.UnitOfWork>();
        services.AddScoped<IAppSettingService, AppSettingService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
