using DM.Domain.Core.Authorization;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Authorization;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Messaging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DM.Infrastructure.Mail;

namespace DM.Workers.Mail;

/// <summary>
/// Mail worker host configuration
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
            .AddDmMessageQueuing(_configuration)
            .AddDmMailConfiguration(_configuration)
            .AddDmLogging("DM.MailSender.Consumer", _configuration, _environment);

        services.AddDmRetryingConsumer(MailConsumer.QueueName);
        services.AddDmConsumerMiddleware<RetryingConsumerMiddleware>();

        services.AddHostedService<MailConsumer>();

        services.AddDmBrokerHealthCheck(_configuration, ["messaging", "ready"]);

        services.AddScoped<IAuthorizationContextProvider, NoAuthorizationContextProvider>();

        // After the explicit registrations: the scans only fill gaps.
        services
            .AddDmMessaging()
            .AddDmCore()
            .AddDefaultTypes(typeof(Startup).Assembly);
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    public void Configure(IApplicationBuilder applicationBuilder)
    {
        applicationBuilder.UseDmWorkerEndpoints();
    }
}
