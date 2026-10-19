using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Configuration this module needs in order to work at all.
/// </summary>
/// <remarks>
/// One call for every host, so no host binds the section without validating it.
/// </remarks>
public static class MessagingConfigurationExtensions
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
            // Absolute: `new Uri(...)` throws on a relative value, on the first message.
            .Validate(
                r => Uri.TryCreate(r.Endpoint, UriKind.Absolute, out _),
                "RabbitMqConfiguration:Endpoint must be an absolute URI, for example amqp://host:5672")
            // Left empty, the client connects as the default user and fails on the first publish.
            .Validate(r => !string.IsNullOrWhiteSpace(r.Username),
                "RabbitMqConfiguration:Username is required")
            .Validate(r => !string.IsNullOrWhiteSpace(r.Password),
                "RabbitMqConfiguration:Password is required")
            .Validate(r => !string.IsNullOrWhiteSpace(r.VirtualHost),
                "RabbitMqConfiguration:VirtualHost is required, use / for the default one")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Declares one middleware of the consumer pipeline of this host.
    /// </summary>
    /// <remarks>
    /// TryAdd: a middleware registered by its own extension first (the retrying one) keeps that registration.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    public static IServiceCollection AddDmConsumerMiddleware<TMiddleware>(this IServiceCollection services)
        where TMiddleware : class, IConsumerMiddleware
    {
        services.TryAddTransient<TMiddleware>();
        services.AddSingleton(new ConsumerMiddlewareRegistration(typeof(TMiddleware)));
        return services;
    }

    /// <summary>
    /// Registers the retrying consumer middleware of a host, for the queue it reads.
    /// </summary>
    /// <remarks>
    /// The consumer resolves a middleware by type alone, so the queue has to be in the graph.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="queue">Queue this host consumes, as the metrics label it.</param>
    public static IServiceCollection AddDmRetryingConsumer(
        this IServiceCollection services, string queue) =>
        services.AddTransient(provider => new RetryingConsumerMiddleware(
            queue, provider.GetRequiredService<ILogger<RetryingConsumerMiddleware>>()));

    /// <summary>
    /// Adds a health check that actually reaches the broker.
    /// </summary>
    /// <remarks>
    /// The probe uses the connection of the host, so it answers about the session the host depends on.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the endpoint from.</param>
    /// <param name="tags">Tags the host files this check under.</param>
    public static IServiceCollection AddDmBrokerHealthCheck(
        this IServiceCollection services, IConfiguration configuration, IEnumerable<string> tags)
    {
        var rabbitMq = configuration.GetSection(nameof(RabbitMqConfiguration)).Get<RabbitMqConfiguration>()
            ?? new RabbitMqConfiguration();

        // A bad endpoint is reported by AddDmMessageQueuing; a test host without one gets no probe.
        if (!Uri.TryCreate(rabbitMq.Endpoint, UriKind.Absolute, out _))
        {
            return services;
        }

        services.AddHealthChecks()
            .AddRabbitMQ(
                provider => provider.GetRequiredService<DmBrokerConnection>()
                    .GetOpenConnection(CancellationToken.None),
                name: "rabbitmq",
                tags: tags);

        return services;
    }

    /// <summary>
    /// Declares the exchanges this host publishes to, before it publishes to them.
    /// </summary>
    /// <remarks>
    /// Named by the host, not derived from the producers; an architecture gate checks the list.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="exchangeNames">Exchanges this host publishes to.</param>
    public static IServiceCollection AddDmPublishedExchanges(
        this IServiceCollection services, params string[] exchangeNames) =>
        services.AddHostedService(provider => new PublishedExchangeDeclaration(
            provider.GetRequiredService<DmBrokerConnection>(),
            provider.GetRequiredService<ILogger<PublishedExchangeDeclaration>>(),
            exchangeNames));
}
