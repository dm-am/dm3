using Autofac;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Domain.Account.Features.Identity;
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

        services.AddHostedService<SearchIndexerConsumer>();

        services.AddDbContext<DmDbContext>(options => options
            .UseNpgsql(_configuration.GetConnectionString(nameof(ConnectionStrings.Rdb))));

        services.AddHealthChecks();

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

        // The gRPC interceptor sets the caller identity for the duration of a
        // call. Nothing registered IIdentitySetter here, so the interceptor could
        // not be constructed at all and every search request ended as a 503.
        // Registered by hand rather than left to the assembly scan: the scan is
        // per-dependency, and the setter and the provider have to be one instance
        // within a scope.
        builder.RegisterType<IdentityProvider>()
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();
    }

    /// <summary>
    /// Ready to work
    /// </summary>
    /// <param name="applicationBuilder"></param>
    /// <param name="logger"></param>
    public void Configure(IApplicationBuilder applicationBuilder,
        ILogger<Startup> logger)
    {
        applicationBuilder.UseDmWorkerEndpoints(route =>
        {
            route.MapGrpcService<SearchEngineService>();
            route.MapGrpcReflectionService();
        });
    }
}