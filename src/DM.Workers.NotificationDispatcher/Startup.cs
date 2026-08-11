using Autofac;
using DM.Domain.Community.Authorization;
using DM.Domain.Personal.Authorization;
using DM.Infrastructure.Core;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Mail.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Messaging;
using DM.Workers.NotificationDispatcher.Implementation;
using DM.Workers.NotificationDispatcher.Implementation.Bot;
using DM.Workers.NotificationDispatcher.Implementation.Email;
using Jamq.Client.Abstractions.Consuming;
using DM.Domain.Moderation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DM.Domain.Account;

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
        // AddDmAccountConfiguration is here because ConfigureContainer registers
        // the whole account domain below. Its types read four option sections
        // that this host bound none of, and IOptions of an unbound type hands
        // out a default instead of throwing.
        services
            .AddOptions()
            .AddDmCoreConfiguration(_configuration)
            .AddDmMessageQueuing(_configuration)
            .AddDmMailConfiguration(_configuration)
            .AddDmAccountConfiguration(_configuration)
            .AddDmLogging("DM.Notifications.Consumer", _configuration, _environment)
            .RequireRelationalStorage()
            .RequireDocumentStorage();

        services.AddDmJamqClient(
            consumerBuilderDefaults: builder => builder.WithMiddleware<NotificationConsumerRetryMiddleware>());
        services.AddHostedService<NotificationDispatcherConsumer>();

        services.AddDmBrokerHealthCheck(_configuration);

        services
            .AddDbContext<DmDbContext>(options => options
                .UseNpgsql(_configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))))
            .AddHttpClient()
            .AddMvc();
    }

    /// <summary>
    /// Configure application container
    /// </summary>
    /// <param name="builder">Container builder</param>
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();

        builder.RegisterModuleOnce<CoreModule>();
        builder.RegisterModuleOnce<PersistenceModule>();
        builder.RegisterModuleOnce<MessageQueuingModule>();
        // Register Domain.Personal types (PersonalModule was removed)
        var personalAssembly = typeof(UserIntention).Assembly;
        builder.RegisterDefaultTypes(personalAssembly);
        builder.RegisterMapper(personalAssembly);
        // Register Domain.Community types (CommunityModule was removed)
        var communityAssembly = typeof(PollIntention).Assembly;
        builder.RegisterDefaultTypes(communityAssembly);
        builder.RegisterMapper(communityAssembly);

        // IIdentityProvider — needed by NotificationService for the Read/Mark methods,
        // which are never called in the worker context (only CreateAsync
        // is used). Register the same IdentityProvider as in the API
        // so DI can resolve the constructor; Current stays null until first
        // access (which never happens in the worker).
        var accountAssembly = typeof(DM.Domain.Account.Authorization.AccountIntention).Assembly;
        builder.RegisterDefaultTypes(accountAssembly);
        builder.RegisterMapper(accountAssembly);
        builder.RegisterModuleOnce<DM.Domain.Account.AccountModule>();

        builder.RegisterModuleOnce<MailModule>();

        // Email notification sender
        builder.RegisterType<NotificationEmailSender>()
            .As<INotificationEmailSender>()
            .InstancePerLifetimeScope();

        // Bot notification sender (Discord/Telegram)
        builder.RegisterType<NotificationBotSender>()
            .As<INotificationBotSender>()
            .InstancePerLifetimeScope();

        // The producer is a type of its own rather than a field the processor builds.
        // Jamq opens a scope per delivered message and resolves the processor in it,
        // so a producer constructed by the processor took an AMQP channel per message
        // and never gave it back: a channel returns to the pool only in Dispose, and
        // the processor was not IDisposable. Here a scope lives for one message, so
        // Dispose is the load-bearing half rather than the lifetime. The scope matches
        // the API, where several services ask for one producer within a request.
        builder.RegisterType<RealtimeNotificationProducer>()
            .As<IRealtimeNotificationProducer>()
            .InstancePerLifetimeScope();
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
