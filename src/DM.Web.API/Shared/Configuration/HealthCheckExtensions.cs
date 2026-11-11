using DM.Web.API.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using DM.Infrastructure.Core.Configuration;

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
    public static IServiceCollection AddDmHealthChecks(this IServiceCollection services)
    {
        services.AddSingleton<PushListenerState>();
        services.AddHealthChecks()
            .AddDmDatabaseCheck()
            // Whether the listener listens now. Degraded and not
            // ready: an instance without push serves every request.
            .AddCheck<PushListenerState>("realtime-push", HealthStatus.Degraded, [MessagingTag]);

        return services;
    }
}
