using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static partial class DependencyInjection
{
    public static void AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddApiAuthentication();
        services.AddMemoryCache();
        services.AddPersistence(configuration);
        services.AddClients();
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    }
}
