using Autofac;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Domain.Core.Extensions;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Logging;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Messaging;
using DM.Domain.Core.Search;
using DM.Workers.SearchIndexer.Implementation;
using DM.Workers.SearchIndexer.Interceptors;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Workers.SearchIndexer;

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
        builder.RegisterModuleOnce<PersistenceModule>();
        builder.RegisterModuleOnce<MessageQueuingModule>();
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