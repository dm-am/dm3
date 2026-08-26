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
/// It ships next to <see cref="MessagingRegistrationExtensions"/> so that
/// registering the module and binding its options are one call. Written out by each host
/// instead, it went wrong the way it always goes wrong: the API validated the
/// endpoint and refused to start without one, while both workers bound the
/// section and validated nothing. A worker with an empty endpoint started,
/// reported itself healthy — its health check probes nothing — and threw
/// UriFormatException on the first message it was handed.
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
            // Absolute, not merely non-empty: the endpoint is handed to
            // `new Uri(...)`, which throws on a relative value, and that throw
            // would happen on the first message rather than at startup.
            .Validate(
                r => Uri.TryCreate(r.Endpoint, UriKind.Absolute, out _),
                "RabbitMqConfiguration:Endpoint must be an absolute URI, for example amqp://host:5672")
            // The other three travel as fields rather than inside the endpoint, so
            // nothing about the endpoint says whether they are there. Left empty
            // they are not refused by the client either: it connects as the
            // anonymous default and fails on the first publish, at which point the
            // producer swallows the refusal by design and the site goes quiet.
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
    /// A host is what decides whether the messages it takes off its queue are
    /// worth another attempt. All three hosts consume: the two workers declare
    /// the same <see cref="RetryingConsumerMiddleware"/> and differ in the queue
    /// they register it for; the API declares a middleware of its own, which
    /// leaves the retry out for the reason that file states. The declaration
    /// lives in the host's composition so the pipeline of a host is read where
    /// the host is read.
    ///
    /// TryAdd rather than Add, because a middleware whose construction needs
    /// more than the container gives — the retrying one takes its queue as a
    /// string — is registered by its own extension first, and that registration
    /// must not be replaced with one the container cannot activate.
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
    /// The consumer resolves a middleware out of the message scope by its type
    /// and hands it nothing of its own, so the queue cannot travel with the
    /// pipeline declaration - it has to be in the graph. Which is also what lets
    /// one middleware serve both workers: the queue was the only thing their two
    /// copies did not share.
    ///
    /// Per resolution, so what the middleware builds outlives no message scope.
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
    /// For a consumer host this is the whole of its health: it exists to take
    /// messages off a queue. Both workers called a bare <c>AddHealthChecks()</c>,
    /// which registers the endpoint with nothing behind it and answers Healthy
    /// unconditionally — including while the process was failing every message
    /// it was handed.
    ///
    /// The probe opens a channel on the same connection the application
    /// publishes and consumes on. That is the point rather than a shortcut: a
    /// probe that dials a connection of its own answers about the broker in
    /// general, while this one answers about the session everything in the host
    /// actually depends on — credentials, virtual host and all.
    /// </remarks>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Configuration to read the endpoint from.</param>
    /// <param name="tags">Tags the host files this check under.</param>
    public static IServiceCollection AddDmBrokerHealthCheck(
        this IServiceCollection services, IConfiguration configuration, IEnumerable<string> tags)
    {
        var rabbitMq = RabbitMqConfiguration.From(configuration);

        // A misconfigured endpoint is reported by AddDmMessageQueuing, whose
        // ValidateOnStart names the setting and says what a good value looks
        // like. A host with no endpoint at all - a test host, typically - gets
        // no broker probe rather than a permanently red one.
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
    /// Named by the host rather than derived from the producers, because a
    /// producer is built in a scope and this is a decision about topology - the
    /// same place the consumers declare theirs. The list of a host is asserted
    /// against its producers by an architecture gate.
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
