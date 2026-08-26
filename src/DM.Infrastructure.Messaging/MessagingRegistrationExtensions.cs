using DM.Infrastructure.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Services of the messaging module.
/// </summary>
public static class MessagingRegistrationExtensions
{
    /// <summary>
    /// Registers the broker plumbing and the assembly's default types.
    /// </summary>
    /// <remarks>
    /// IEventProducer is deliberately not registered here: publishing an event
    /// is an outbox INSERT since W1.5, so the implementation needs DmDbContext
    /// and lives with the persistence module. This module keeps the transport
    /// half - InvokedEvent, the exchange name and the producer plumbing the
    /// relay builds on in the host.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmMessaging(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectionFactory>(provider =>
            provider.GetRequiredService<IOptions<RabbitMqConfiguration>>().Value.CreateConnectionFactory());

        // One connection per host, disposed with the container. Everything the
        // host does against the broker - producers, consumers, topology, the
        // health probe - opens channels on this one; see DmBrokerConnection.
        services.TryAddSingleton<DmBrokerConnection>();

        services.TryAddSingleton<IDmProducerBuilder, DmProducerBuilder>();

        // Scopes are opened per delivered message through the root factory, and
        // the middleware registrations arrive from the host's service
        // collection - a host that consumes nothing has declared none.
        services.TryAddSingleton<IDmConsumerBuilder>(provider => new DmConsumerBuilder(
            provider.GetRequiredService<DmBrokerConnection>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetServices<ConsumerMiddlewareRegistration>(),
            provider.GetRequiredService<ILoggerFactory>()));

        services.AddDefaultTypes(typeof(MessagingRegistrationExtensions).Assembly);

        return services;
    }
}
