using DM.Domain.Community.Authorization;
using DM.Domain.Personal.Authorization;
using DM.Infrastructure.Core;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Infrastructure.Messaging.GeneralBus;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Email;
using DM.Domain.Moderation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Workers.NotificationDispatcher;

/// <summary>
/// Notification dispatcher host configuration
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    ///
    /// </summary>
    /// <param name="configuration"></param>
    /// <param name="environment">The host's answer about the environment, so logging cannot give a second one</param>
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    ///
    /// </summary>
    /// <param name="services"></param>
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions()
            .AddDmCoreConfiguration(_configuration)
            .AddDmMessageQueuing(_configuration)
            .AddDmMailConfiguration(_configuration)
            .AddDmLogging("DM.Notifications.Consumer", _configuration, _environment)
            // Every letter and both bot messages this host builds carry the way back
            // to what they are about, and the root of that link is the address this
            // deployment answers on. Empty, it builds no link and says so nowhere,
            // which is the failure the declaration exists to refuse.
            .RequireGeneratedLinks()
            .RequireRelationalStorage();

        services.AddDmRetryingConsumer(NotificationDispatcherConsumer.QueueName);
        services.AddDmConsumerMiddleware<RetryingConsumerMiddleware>();
        services.AddHostedService<NotificationDispatcherConsumer>();

        // The two exchanges this host publishes to. Same reason as in the API:
        // the client declares nothing on the producing side, so without this they
        // exist only for as long as somebody has consumed them.
        services.AddDmPublishedExchanges(
            MailTransport.ExchangeName, RealtimeNotificationsTransport.ExchangeName);

        services.AddDmBrokerHealthCheck(_configuration, ["messaging", "ready"]);

        // No EnableRetryOnFailure here, unlike the API: this host retries the
        // message rather than the query. RetryingConsumerMiddleware, registered
        // above, replays the whole handler five times over 62 seconds, which
        // outlasts a database restart, and everything the handler touches in
        // Postgres is a read - so a replay costs nothing and covers more than a
        // per-query retry would. The one read outside that cover is the address
        // lookup inside a best-effort delivery, and losing it costs a letter and
        // not the notification: that is written to the document store before any
        // delivery starts.
        services
            .AddDbContext<DmDbContext>(options => options
                .UseNpgsql(_configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))))
            .AddHttpClient()
            .AddMvc();

        // IIdentityProvider, declared by NotificationService for its read and mark
        // methods, which this host never calls - it only creates. The whole account
        // domain used to be scanned in for that one interface, and it brought its
        // option sections, its mapper profiles and an authorization context
        // answering Guest with it. One type instead, and it refuses rather than
        // answers - both faces of it: see NoIdentityProvider.
        services.AddScoped<NoIdentityProvider>();
        services.AddScoped<IIdentityProvider>(
            provider => provider.GetRequiredService<NoIdentityProvider>());
        services.AddScoped<DM.Domain.Core.Authorization.IAuthorizationContextProvider>(
            provider => provider.GetRequiredService<NoIdentityProvider>());

        // Same shape for the realtime push the scanned user service declares:
        // the hub lives in the API, so this host refuses rather than skips.
        services.AddScoped<DM.Domain.Personal.Features.Profiles.IRealtimeAvatarBroadcaster,
            NoRealtimeAvatarBroadcaster>();

        // Email notification sender
        services.AddScoped<INotificationEmailSender, NotificationEmailSender>();

        // Bot notification sender (Discord/Telegram)
        services.AddScoped<INotificationBotSender, NotificationBotSender>();

        // The producer is a type of its own rather than a field the processor builds.
        // The consumer opens a scope per delivered message and resolves the processor
        // in it, so a producer constructed by the processor opened an AMQP channel per
        // message and never closed it: a channel closes only in Dispose, and the
        // processor was not IDisposable. Here a scope lives for one message, so
        // Dispose is the load-bearing half rather than the lifetime. The scope matches
        // the API, where several services ask for one producer within a request.
        services.AddScoped<IRealtimeNotificationProducer, RealtimeNotificationProducer>();

        // The DI modules and the scans, after everything the host wires
        // explicitly: the scans only fill gaps, so the DbContext and the
        // registrations above must already be on the collection when they run.
        services
            .AddDmMail()
            .AddDmMessaging()
            .AddDmCore()
            .AddDmPersistence()
            .AddDefaultTypes(typeof(Startup).Assembly)
            // Domain.Personal and Domain.Community types (their modules were removed)
            .AddDefaultTypes(typeof(UserIntention).Assembly)
            .AddDefaultTypes(typeof(PollIntention).Assembly);
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    /// <param name="applicationBuilder"></param>
    public void Configure(IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseDmWorkerEndpoints(route => route.MapControllers());
    }
}
