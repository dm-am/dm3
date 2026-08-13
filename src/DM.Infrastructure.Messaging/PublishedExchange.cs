using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Declaration of the exchanges this host publishes to.
/// </summary>
/// <remarks>
/// Nothing on the publishing side used to declare anything: the client has a
/// call for it and never makes it, and the exchanges existed only because the
/// consumers declare what they subscribe to. So on a stand where a worker had
/// never started, every publish went to an exchange that was not there — and
/// with no publisher confirms asked for, the broker's refusal reached nobody.
/// A registration answered 200 with its letter dropped on the floor.
///
/// Declared to match, argument for argument, what the consumers declare and what
/// a running broker reports: a durable topic exchange that is not auto-deleted.
/// A declaration that disagrees is answered with 406 and closes the channel, so
/// this is not a place to improve on the settings.
/// </remarks>
public static class PublishedExchange
{
    /// <summary>Type every working exchange of this system is declared as.</summary>
    private const string Type = ExchangeType.Topic;

    /// <summary>
    /// Declares the given exchanges, durable and not auto-deleted.
    /// </summary>
    /// <param name="connectionFactory">Broker connection factory.</param>
    /// <param name="exchangeNames">Exchanges this host publishes to.</param>
    public static void Declare(IAsyncConnectionFactory connectionFactory, params string[] exchangeNames)
    {
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        foreach (var exchangeName in exchangeNames)
        {
            channel.ExchangeDeclare(exchangeName, Type, durable: true, autoDelete: false);
        }
    }
}

/// <summary>
/// Declares the exchanges of a host before anything publishes to them.
/// </summary>
/// <remarks>
/// A hosted service rather than a line in the producer: a producer is built per
/// scope and would pay a broker round trip on every request to say a thing that
/// is true after the first one. A host declares its topology once, the way the
/// consumers already do.
///
/// It does not stop the host when the broker is unreachable, for the same reason
/// the consumers do not: the site serves every page without a broker, and
/// letting this escape would turn an outage of it into a crash loop. What it
/// costs instead is that publishing stays broken until the next restart, which
/// is the state the counter of failed publishes is read for.
/// </remarks>
public class PublishedExchangeDeclaration : BackgroundService
{
    private readonly IAsyncConnectionFactory _connectionFactory;
    private readonly ILogger<PublishedExchangeDeclaration> _logger;
    private readonly string[] _exchangeNames;

    /// <inheritdoc cref="PublishedExchangeDeclaration" />
    /// <param name="connectionFactory">Broker connection factory.</param>
    /// <param name="logger">Logger of the host.</param>
    /// <param name="exchangeNames">Exchanges this host publishes to.</param>
    public PublishedExchangeDeclaration(
        IAsyncConnectionFactory connectionFactory,
        ILogger<PublishedExchangeDeclaration> logger,
        IEnumerable<string> exchangeNames)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        _exchangeNames = exchangeNames.ToArray();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Everything before the first await runs inside host startup, so a broker
        // that is not up yet would abort the host before its own health check
        // could report why. Both consumers open the same way.
        await Task.Yield();

        var retry = Policy.Handle<Exception>().WaitAndRetryAsync(5,
            attempt => TimeSpan.FromSeconds(1 << attempt),
            (exception, _) => _logger.LogWarning(exception,
                "Could not declare the exchanges this host publishes to"));

        try
        {
            await retry.ExecuteAsync(_ =>
            {
                PublishedExchange.Declare(_connectionFactory, _exchangeNames);
                return Task.CompletedTask;
            }, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "[💥] Could not declare {Exchanges}; publishing to them fails until this host " +
                "restarts or a consumer of them declares them first",
                string.Join(", ", _exchangeNames));
        }
    }
}
