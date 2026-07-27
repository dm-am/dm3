using Autofac;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Mail.Configuration;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Messaging;
using DM.Workers.Mail;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Workers.Mail;

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
    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddOptions()
            .Configure<EmailConfiguration>(_configuration.GetSection(nameof(EmailConfiguration)).Bind)
            .Configure<ConnectionStrings>(_configuration.GetSection(nameof(ConnectionStrings)).Bind)
            .Configure<RabbitMqConfiguration>(_configuration.GetSection(nameof(RabbitMqConfiguration)).Bind)
            .AddDmLogging("DM.MailSender.Consumer", _configuration);

        services.AddJamqClient(
            config => config.UseRabbit(),
            consumerBuilderDefaults: builder => builder.WithMiddleware<ConsumerRetryMiddleware>());

        services.AddHostedService<MailConsumer>();

        services.AddHealthChecks();

        services.AddMvc();
    }

    /// <summary>
    /// Configure application container
    /// </summary>
    /// <param name="builder">Container builder</param>
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();

        builder.RegisterModuleOnce<CoreModule>();
        builder.RegisterModuleOnce<MessageQueuingModule>();
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