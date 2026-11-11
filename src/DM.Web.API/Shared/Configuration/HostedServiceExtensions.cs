using DM.Infrastructure.Core;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Web.API.HostedServices;
using DM.Web.API.Realtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Background services of the API host.
/// </summary>
internal static class HostedServiceExtensions
{
    /// <summary>
    /// Whether this instance runs the periodic jobs: the one reading of the
    /// setting and of its default.
    /// </summary>
    /// <param name="configuration">Configuration to read the setting from.</param>
    internal static bool PeriodicServicesEnabled(IConfiguration configuration) =>
        configuration.GetValue("PeriodicServices:Enabled", true);

    /// <summary>
    /// Registers everything the host runs alongside serving requests: the
    /// push listener, the schema assertions and, when asked, the periodic jobs.
    /// </summary>
    /// <remarks>
    /// Nothing here is registered in migration mode: that container applies the
    /// migration and exits, it has no S3 access, and a cleanup job that starts
    /// and is torn down mid-pass is worse than one that never started. The twin
    /// of a rollout serves requests next to the primary and runs no periodic job,
    /// since one started on both instances runs twice.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="runPeriodicServices">Whether the periodic jobs start on this instance.</param>
    public static IServiceCollection AddDmHostedServices(this IServiceCollection services, bool runPeriodicServices)
    {
        // The listener resolves the push handler in a scope of its own per push.
        services.AddScoped<RealtimeNotificationProcessor>();
        services.AddHostedService<PushListener>();
        services.AddHostedService<WarmupService>();

        // Asserts the relational indexes EF cannot express in the model. Keeping
        // them out of the migration is what lets the migration stay generated.
        services.AddHostedService<ExpressionIndexInitializer>();

        // Read on the scrape, apart from every loop, so a stalled one cannot freeze them.
        services.AddDmQueueMetrics();

        if (!runPeriodicServices)
        {
            return services;
        }

        services.AddHostedService<TokenCleanupService>();
        services.AddHostedService<SessionCleanupService>();
        services.AddHostedService<RetentionSweepService>();
        services.AddHostedService<PendingRegistrationCleanupService>();
        services.AddHostedService<TwoFactorMaintenanceService>();
        services.AddHostedService<PeriodDigestService>();
        services.AddHostedService<UsernameChangeCleanupService>();
        services.AddHostedService<PendencyReminderService>();
        services.AddHostedService<GameInactivityService>();
        services.AddHostedService<PopularityScoreService>();
        services.AddHostedService<UploadOrphanCleanupService>();

        // This host sends the sign-in letter that names a country, so this host
        // keeps the table it names it by fresh.
        services.AddDmCountryTableRefresh();

        return services;
    }
}
