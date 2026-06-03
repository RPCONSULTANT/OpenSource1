using Microsoft.EntityFrameworkCore;
using test.Data.Repositories;
using test.Data.UnitOfWork;
using test.Identity;

namespace test.Data;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationData(this IServiceCollection services, IConfiguration configuration)
    {
        var applicationConnectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        var identityConnectionString = configuration.GetConnectionString("IdentityConnection")
            ?? throw new InvalidOperationException("Connection string 'IdentityConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(applicationConnectionString));

        services.AddDbContext<AppIdentityDbContext>(options =>
            options.UseSqlServer(identityConnectionString));

        services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork.UnitOfWork>();

        return services;
    }
}
