using System;
using Jamq.Client.Abstractions.Consuming;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.DependencyInjection;
using Jamq.Client.Rabbit.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Configuration this module needs in order to work at all.
/// </summary>
/// <remarks>
/// It ships next to <see cref="MessageQueuingModule"/> so that registering the
/// module and binding its options are one call. Written out by each host
/// instead, it went wrong the way it always goes wrong: the API validated the
/// endpoint and refused to start without one, while both workers bound the
/// section and validated nothing. A worker with an empty endpoint started,
/// reported itself healthy — its health check probes nothing — and threw
/// UriFormatException on the first message it was handed.
/// </remarks>
public static class MessageQueuingConfigurationExtensions
{
    /// <summary>
    /// Binds and validates the broker connection parameters.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the section from.</param>
    public static IServiceCollection AddDmMessageQueuing(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqConfiguration>()
            .Bind(configuration.GetSection(nameof(RabbitMqConfiguration)))
            // Absolute, not merely non-empty: the endpoint is handed to
            // `new Uri(...)`, which throws on a relative value, and that throw
            // would happen on the first message rather than at startup.
            .Validate(
                r => Uri.TryCreate(r.Endpoint, UriKind.Absolute, out _),
                "RabbitMqConfiguration:Endpoint must be an absolute URI, for example amqp://host:5672")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Registers the message queuing client with the defaults every host shares.
    /// </summary>
    /// <remarks>
    /// The producer side is what this call exists for. Each host used to register
    /// the client on its own, and a producer default written in one of them would
    /// have been absent from the other two — persistence has to hold for every
    /// publisher in the system or the queue it protects is emptied by whichever
    /// process forgot it. The consumer pipeline is named by the host, because a
    /// host is what decides whether what it takes off its queue is worth another
    /// attempt. All three hosts consume: the two workers off the queues they were
    /// written for, and the API off the realtime push. The workers install the
    /// same <see cref="RetryingConsumerMiddleware"/> and differ in the queue they
    /// register it for; the API passes one of its own, which leaves the retry out
    /// for the reason that file states.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="consumerBuilderDefaults">Consumer pipeline of this host, if it consumes at all.</param>
    public static IServiceCollection AddDmJamqClient(
        this IServiceCollection services,
        Func<IConsumerBuilder, IConsumerBuilder>? consumerBuilderDefaults = null) =>
        services
            .AddTransient<PersistentDeliveryMiddleware>()
            .AddJamqClient(
                config => config.UseRabbit(),
                producerBuilderDefaults: builder => builder.WithMiddleware<PersistentDeliveryMiddleware>(),
                consumerBuilderDefaults: consumerBuilderDefaults);

    /// <summary>
    /// Registers the retrying consumer middleware of a host, for the queue it reads.
    /// </summary>
    /// <remarks>
    /// The client resolves an interface middleware out of the container by its type
    /// and hands it nothing of its own, so the queue cannot travel with the pipeline
    /// declaration - it has to be in the graph. Which is also what lets one middleware
    /// serve both workers: the queue was the only thing their two copies did not
    /// share.
    ///
    /// Per resolution, the way the assembly scan used to hand out those copies. A
    /// message is handled in a scope of its own, and what the middleware builds
    /// outlives none of them.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="queue">Queue this host consumes, as the metrics label it.</param>
    public static IServiceCollection AddDmRetryingConsumer(
        this IServiceCollection services, string queue) =>
        services.AddTransient(provider => new RetryingConsumerMiddleware(
            queue, provider.GetRequiredService<ILogger<RetryingConsumerMiddleware>>()));

    /// <summary>
    /// Adds a health check that actually opens a connection to the broker.
    /// </summary>
    /// <remarks>
    /// For a consumer host this is the whole of its health: it exists to take
    /// messages off a queue. Both workers called a bare <c>AddHealthChecks()</c>,
    /// which registers the endpoint with nothing behind it and answers Healthy
    /// unconditionally — including while the process was failing every message
    /// it was handed.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the endpoint from.</param>
    public static IServiceCollection AddDmBrokerHealthCheck(
        this IServiceCollection services, IConfiguration configuration)
    {
        var rabbitMq = new RabbitMqConfiguration();
        configuration.GetSection(nameof(RabbitMqConfiguration)).Bind(rabbitMq);

        // A misconfigured endpoint is reported by AddDmMessageQueuing, whose
        // ValidateOnStart names the setting and says what a good value looks
        // like. That runs when the host starts, which is after this method — so
        // constructing the Uri unguarded here would pre-empt it with a bare
        // UriFormatException and no mention of which setting is at fault.
        if (!Uri.TryCreate(rabbitMq.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return services;
        }

        services.AddHealthChecks()
            .AddRabbitMQ(
                rabbitConnectionString: endpoint,
                name: "rabbitmq",
                tags: new[] { "messaging", "ready" });

        return services;
    }
}
