using System;
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
    /// <summary>
    /// Registers a check per backing store the host depends on.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Source of the connection strings to probe.</param>
    public static IServiceCollection AddDmHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionStrings = new ConnectionStrings();
        configuration.GetSection(nameof(ConnectionStrings)).Bind(connectionStrings);
        services.AddHealthChecks()
            .AddNpgSql(
                connectionString: connectionStrings.Rdb,
                name: "postgresql",
                tags: new[] { "db", "ready" });

        // Not "ready" here, unlike in the consumer workers: readiness answers
        // "can this instance serve a request", and every request is served
        // without the broker — publishing an event is fire-and-forget alongside
        // the response. Tagging it ready pulled the whole site out of rotation
        // over a delayed notification. A worker has the opposite answer: taking
        // messages off a queue is all it does.
        //
        // Through the shared registration so the probe connects as the user the
        // client connects as. Given a bare endpoint it fell back to the library
        // defaults and reported Healthy about a broker nobody could log in to.
        services.AddDmBrokerHealthCheck(configuration, ["messaging"]);

        // A reachable broker and a subscribed consumer are two different states,
        // and the check above answers only the first: the connection opens, the
        // probe passes, and the queue the push consumer never declared is a queue
        // nobody can ask about. So the consumer reports its own subscription, and
        // this host is the only place that can.
        //
        // Registered here rather than beside the hosted service because the holder
        // exists for this check; both ends of it are one decision. Degraded and
        // untagged, so /_ready keeps ignoring it and the Docker health check on
        // /_health, which runs no check at all, is untouched: an instance without
        // push serves every request.
        services.AddSingleton<RealtimeSubscriptionState>();
        services.AddHealthChecks()
            .Add(new HealthCheckRegistration(
                "realtime-push",
                provider => provider.GetRequiredService<RealtimeSubscriptionState>(),
                HealthStatus.Degraded,
                ["messaging"]));

        return services;
    }
}
