using Autofac;
using DM.Services.Core;
using DM.Services.Core.Configuration;
using DM.Services.Core.Extensions;
using DM.Services.Core.Logging;
using DM.Services.DataAccess;
using DM.Services.MessageQueuing;
using DM.Services.Search.Configuration;
using DM.Services.Search.Consumer.Implementation;
using DM.Services.Search.Consumer.Interceptors;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Services.Search.Consumer;

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
            .Configure<SearchEngineConfiguration>(_configuration.GetSection(nameof(SearchEngineConfiguration)).Bind)
            .AddDmLogging("DM.Search.Consumer", _configuration);

        services.AddJamqClient(
            config => config.UseRabbit(),
            consumerBuilderDefaults: builder => builder.WithMiddleware<SearchConsumerRetryMiddleware>());

        services.AddHostedService<SearchEngineConsumer>();

        services.AddDbContext<DmDbContext>(options => options
            .UseNpgsql(_configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))));

        services.AddMvc();
        services.AddGrpc(options => options.Interceptors.Add<IdentityInterceptor>());
        services.AddGrpcReflection();
    }

    /// <summary>
    /// Configure application container
    /// </summary>
    /// <param name="builder">Container builder</param>
    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();
        builder.RegisterMapper();

        builder.RegisterModuleOnce<CoreModule>();
        builder.RegisterModuleOnce<DataAccessModule>();
        builder.RegisterModuleOnce<MessageQueuingModule>();
        builder.RegisterModuleOnce<SearchEngineModule>();
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    /// <param name="applicationBuilder"></param>
    /// <param name="logger"></param>
    public void Configure(IApplicationBuilder applicationBuilder,
        ILogger<Startup> logger)
    {
        applicationBuilder
            .UseRouting()
            .UseEndpoints(route =>
            {
                route.MapGrpcService<SearchEngineService>();
                route.MapGrpcReflectionService();
            });
    }
}