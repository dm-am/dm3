using DM.Domain.Community.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Infrastructure.Core;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Core.Authorization;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Mail;
using DM.Workers.NotificationDispatcher.Dispatching;
using DM.Workers.NotificationDispatcher.Bot;
using DM.Workers.NotificationDispatcher.Email;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DM.Workers.NotificationDispatcher;

/// <summary>
/// Notification dispatcher host configuration
/// </summary>
public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    /// <summary>Takes the host's answer about the environment, so logging cannot give a second one.</summary>
    public Startup(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDmCoreConfiguration(_configuration)
            .AddDmMailConfiguration(_configuration)
            .AddDmLogging("DM.Notifications.Consumer", _configuration, _environment)
            // Every letter and bot message links back to the site, and without the address it links nowhere.
            .RequireGeneratedLinks()
            .RequireRelationalStorage();

        // Events are claimed from the outbox table, and letters are stored in it for the mail worker.
        services.AddHostedService(NotificationQueue.Worker);

        // No EnableRetryOnFailure: a failed row is handled again whole on the next pass.
        services
            .AddDmConnectionSource()
            .AddDbContext<DmDbContext>((provider, options) => options
                .UseNpgsql(provider.GetRequiredService<NpgsqlDataSource>()))
            .AddHttpClient();

        // The process's own source, so the check probes the pool the claim and the data layer draw from.
        services.AddHealthChecks()
            .AddNpgSql(
                provider => provider.GetRequiredService<NpgsqlDataSource>(),
                name: "postgresql",
                tags: ["db", "ready"]);

        // NotificationService declares IIdentityProvider for methods this host never calls.
        services.AddScoped<IIdentityProvider, NoIdentityProvider>();
        services.AddScoped<IAuthorizationContextProvider, NoAuthorizationContextProvider>();

        // The hub lives in the API.
        services.AddScoped<IRealtimeAvatarBroadcaster, NoRealtimeAvatarBroadcaster>();

        services.AddScoped<INotificationEmailSender, NotificationEmailSender>();

        services.AddScoped<INotificationBotSender, NotificationBotSender>();

        services.AddScoped<IRealtimeNotificationProducer, RealtimeNotificationProducer>();

        // After the explicit registrations: the scans only fill gaps.
        services
            .AddDmMail()
            .AddDmCore()
            .AddDmPersistence()
            .AddDefaultTypes(typeof(Startup).Assembly)
            // Domain.Personal and Domain.Community have no modules of their own
            .AddDefaultTypes(typeof(UserIntention).Assembly)
            .AddDefaultTypes(typeof(PollIntention).Assembly);
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    public void Configure(IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseDmWorkerEndpoints();
    }
}
