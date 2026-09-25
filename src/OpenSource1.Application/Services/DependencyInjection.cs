using Microsoft.Extensions.DependencyInjection;
using OpenSource1.Application.Common.Behaviors;

namespace OpenSource1.Application.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // El orden importa: logging envuelve todo (mide el tiempo total, incluida la
            // validación y la transacción); la validación corre antes de abrir transacción, para
            // no pagar el costo de abrir una transacción que se va a descartar por datos
            // inválidos; la transacción es lo más cercano al handler, envolviendo solo el
            // trabajo que de verdad toca la base de datos.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            configuration.AddOpenBehavior(typeof(TransactionBehavior<,>));
        });

        return services;
    }
}
