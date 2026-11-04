using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Messaging;
using DM.Web.API.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Health checks of the API host.
/// </summary>
internal static class HealthCheckExtensions
{
    /// <summary>Tag of the checks /_ready answers with.</summary>
    public const string ReadyTag = "ready";

    private const string MessagingTag = "messaging";

    /// <summary>
    /// Registers a check per backing store the host depends on.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Source of the connection strings to probe.</param>
    public static IServiceCollection AddDmHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<RealtimeSubscriptionState>();
        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))!,
                name: "postgresql",
                tags: ["db", ReadyTag])
            // A reachable broker is not a subscribed consumer. Degraded and not
            // ready: an instance without push serves every request.
            .AddCheck<RealtimeSubscriptionState>("realtime-push", HealthStatus.Degraded, [MessagingTag]);

        // Not ready, unlike in the workers: every request is served without the broker.
        services.AddDmBrokerHealthCheck(configuration, [MessagingTag]);

        return services;
    }
}
