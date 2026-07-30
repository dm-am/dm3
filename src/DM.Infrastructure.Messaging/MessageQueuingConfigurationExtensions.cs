using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
