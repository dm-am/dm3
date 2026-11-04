using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Declares the exchanges of a host before anything publishes to them.
/// </summary>
/// <remarks>
/// The producer declares nothing, so without this an exchange exists only once a consumer declared it.
/// Declared exactly as the consumers declare it, or the broker answers 406.
/// Once per host rather than per producer. An unreachable broker is logged, not a crash loop.
/// </remarks>
public class PublishedExchangeDeclaration : BackgroundService
{
    private readonly DmBrokerConnection _connection;
    private readonly ILogger<PublishedExchangeDeclaration> _logger;
    private readonly string[] _exchangeNames;

    /// <inheritdoc cref="PublishedExchangeDeclaration" />
    /// <param name="connection">Broker connection of the host.</param>
    /// <param name="logger">Logger of the host.</param>
    /// <param name="exchangeNames">Exchanges this host publishes to.</param>
    public PublishedExchangeDeclaration(
        DmBrokerConnection connection,
        ILogger<PublishedExchangeDeclaration> logger,
        IEnumerable<string> exchangeNames)
    {
        _connection = connection;
        _logger = logger;
        _exchangeNames = exchangeNames.ToArray();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Everything before the first await runs inside host startup.
        await Task.Yield();

        var retry = BrokerRetry.Create(exception => _logger.LogWarning(exception,
            "Could not declare the exchanges this host publishes to"));

        try
        {
            await retry.ExecuteAsync(Declare, stoppingToken);
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

    private async Task Declare(CancellationToken cancellationToken)
    {
        var open = await _connection.GetOpenConnection(cancellationToken);
        await using var channel = await open.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var exchangeName in _exchangeNames)
        {
            await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: cancellationToken);
        }
    }
}
