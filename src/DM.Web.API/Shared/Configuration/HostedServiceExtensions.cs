using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.RelationalStorage;
using DM.Web.API.HostedServices;
using DM.Web.API.Realtime;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Web.API.Shared.Configuration;

/// <summary>
/// Background services of the API host.
/// </summary>
internal static class HostedServiceExtensions
{
    /// <summary>
    /// Registers everything the host runs alongside serving requests: the
    /// realtime consumer, the schema assertions and the periodic jobs.
    /// </summary>
    /// <remarks>
    /// Nothing here is registered in migration mode: that container applies the
    /// migration and exits, it has no S3 access, and a cleanup job that starts
    /// and is torn down mid-pass is worse than one that never started.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddDmHostedServices(this IServiceCollection services)
    {
        // Nothing on the publishing side used to declare an exchange, so both of
        // these existed only because some consumer had subscribed first. On a
        // stand where a worker had never started, every publish went nowhere and
        // said nothing.
        services.AddDmPublishedExchanges(
            InvokedEventsTransport.ExchangeName, MailTransport.ExchangeName);

        services.AddHostedService<RealtimeNotificationConsumer>();
        services.AddHostedService<WarmupService>();

        // Asserts the Mongo index set. docker/mongo-init.js only runs on the first
        // start of an empty volume, so this is what keeps deployed databases indexed.
        services.AddHostedService<MongoIndexInitializer>();

        // Asserts the relational indexes EF cannot express in the model. Keeping
        // them out of the migration is what lets the migration stay generated.
        services.AddHostedService<ExpressionIndexInitializer>();

        services.AddHostedService<TokenCleanupService>();
        services.AddHostedService<SessionCleanupService>();
        services.AddHostedService<PendingRegistrationCleanupService>();
        services.AddHostedService<PeriodDigestService>();
        services.AddHostedService<UsernameChangeCleanupService>();
        services.AddHostedService<PendencyReminderService>();
        services.AddHostedService<GameInactivityService>();
        services.AddHostedService<PopularityScoreService>();
        services.AddHostedService<UploadOrphanCleanupService>();

        return services;
    }
}
