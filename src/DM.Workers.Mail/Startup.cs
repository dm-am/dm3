using DM.Infrastructure.Core;
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
            .AddDmLogging("DM.MailSender.Consumer", _configuration, _environment);

        services.AddDmRetryingConsumer(MailConsumer.QueueName);
        services.AddDmConsumerMiddleware<RetryingConsumerMiddleware>();

        services.AddHostedService<MailConsumer>();

        // This worker exists to consume from the broker, so a broker it cannot
        // reach is exactly the condition its health has to report. A bare
        // AddHealthChecks() answered Healthy no matter what.
        services.AddDmBrokerHealthCheck(_configuration, ["messaging", "ready"]);

        services.AddMvc();

        // The intention manager arrives with the core module and asks who the
        // authorization is about; this host serves no request, so it refuses
        // rather than answers: see NoAuthorizationContextProvider.
        services.AddScoped<DM.Domain.Core.Authorization.IAuthorizationContextProvider,
            Sending.NoAuthorizationContextProvider>();

        // The DI modules and the host's own scan, after everything the host
        // wires explicitly: the scans only fill gaps behind the registrations
        // above.
        services
            .AddDmMessaging()
            .AddDmCore()
            .AddDefaultTypes(typeof(Startup).Assembly);
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
