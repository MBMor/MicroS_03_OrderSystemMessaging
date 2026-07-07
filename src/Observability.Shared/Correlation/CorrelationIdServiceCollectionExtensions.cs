using Microsoft.Extensions.DependencyInjection;

namespace Observability.Shared.Correlation;

public static class CorrelationIdServiceCollectionExtensions
{
    public static IServiceCollection AddCorrelationId(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICorrelationIdAccessor, CorrelationIdAccessor>();

        return services;
    }
}