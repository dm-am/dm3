using Autofac;
using DM.Services.Core;
using DM.Services.Core.Configuration;
using DM.Services.Core.Extensions;
using DM.Services.Core.Logging;
using DM.Services.DataAccess;
using DM.Services.MessageQueuing;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DM.Services.Notifications.Consumer;

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
            .AddDmLogging("DM.Notifications.Consumer", _configuration);

        services.AddJamqClient(config => config.UseRabbit());
        services.AddHostedService<NotificationConsumer>();

        services.AddHealthChecks();

        services
            .AddDbContext<DmDbContext>(options => options
                .UseNpgsql(_configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))))
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
        builder.RegisterModuleOnce<DataAccessModule>();
        builder.RegisterModuleOnce<MessageQueuingModule>();
        builder.RegisterModuleOnce<NotificationsModule>();
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