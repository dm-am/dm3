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
    /// Registers the broker plumbing.
    /// </summary>
    /// <remarks>
    /// IEventProducer lives with the persistence module: publishing an event is an outbox insert.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <returns>Service collection for chaining.</returns>
    public static IServiceCollection AddDmMessaging(this IServiceCollection services)
    {
        services.TryAddSingleton<IConnectionFactory>(provider =>
            provider.GetRequiredService<IOptions<RabbitMqConfiguration>>().Value.CreateConnectionFactory());

        services.TryAddSingleton<DmBrokerConnection>();

        services.TryAddSingleton<IDmProducerBuilder, DmProducerBuilder>();

        services.TryAddSingleton<IDmConsumerBuilder>(provider => new DmConsumerBuilder(
            provider.GetRequiredService<DmBrokerConnection>(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetServices<ConsumerMiddlewareRegistration>(),
            provider.GetRequiredService<ILoggerFactory>()));

        return services;
    }
}
