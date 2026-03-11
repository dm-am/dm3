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
using DM.Infrastructure.Messaging.Outbox;
using DM.Workers.NotificationDispatcher.Implementation.Bot;
using DM.Workers.NotificationDispatcher.Implementation.Email;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Workers.NotificationDispatcher;

/// <summary>
/// Search consumer API configuration
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;

    /// <summary>
    ///
    /// </summary>
    /// <param name="configuration"></param>
    public Startup(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions()
            .Configure<ConnectionStrings>(_configuration.GetSection(nameof(ConnectionStrings)).Bind)
            .Configure<RabbitMqConfiguration>(_configuration.GetSection(nameof(RabbitMqConfiguration)).Bind)
            .Configure<BotConfiguration>(_configuration.GetSection(nameof(BotConfiguration)).Bind)
            .Configure<OutboxConfiguration>(_configuration.GetSection(nameof(OutboxConfiguration)).Bind)
            .Configure<EmailConfiguration>(_configuration.GetSection(nameof(EmailConfiguration)).Bind)
            .AddDmLogging("DM.Notifications.Consumer", _configuration);

        services.AddJamqClient(
            config => config.UseRabbit(),
            consumerBuilderDefaults: builder => builder.WithMiddleware<NotificationConsumerRetryMiddleware>());
        services.AddHostedService<NotificationDispatcherConsumer>();

        services.AddHealthChecks();

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
        builder.RegisterModuleOnce<MailModule>();

        // Email notification sender
        builder.RegisterType<NotificationEmailSender>()
            .As<INotificationEmailSender>()
            .InstancePerLifetimeScope();

        // Bot notification sender (Discord/Telegram)
        builder.RegisterType<NotificationBotSender>()
            .As<INotificationBotSender>()
            .InstancePerLifetimeScope();
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    /// <param name="applicationBuilder"></param>
    public void Configure(IApplicationBuilder applicationBuilder)
    {
        applicationBuilder
            .UseRouting()
            .UseHealthChecks("/_health")
            .UseEndpoints(route => route.MapControllers());
    }
}