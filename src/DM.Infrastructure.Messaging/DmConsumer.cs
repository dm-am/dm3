using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The topology one consumer declares and subscribes to.
/// </summary>
/// <param name="consumerTag">Consumer tag the subscription is announced under.</param>
/// <param name="queueName">Queue this consumer reads.</param>
public sealed class DmConsumerParameters(string consumerTag, string queueName)
{
    /// <summary>Consumer tag the subscription is announced under.</summary>
    public string ConsumerTag { get; } = consumerTag;

    /// <summary>Queue this consumer reads, as the topology and the metrics name it.</summary>
    public string QueueName { get; } = queueName;

    /// <summary>Exchange the queue is bound to.</summary>
    public required string ExchangeName { get; init; }

    /// <summary>Routing keys the queue is bound under.</summary>
    public IReadOnlyCollection<string> RoutingKeys { get; init; } = ["#"];

    /// <summary>
    /// Exchange a message this consumer could not process is rejected into; null drops it.
    /// </summary>
    public string? DeadLetterExchange { get; init; }

    /// <summary>
    /// Declares the queue exclusive to this connection, under a name with a
    /// random suffix, deleted when the connection goes.
    /// </summary>
    /// <remarks>
    /// The suffix lets two instances of a host subscribe; the alert matches such a queue by prefix.
    /// </remarks>
    public bool Exclusive { get; init; }
}

/// <summary>
/// A subscription waiting to be opened.
/// </summary>
public interface IDmConsumer
{
    /// <summary>
    /// Declares the topology and subscribes. The messages then arrive on the
    /// client's dispatcher until the connection of the host closes.
    /// </summary>
    Task Subscribe(CancellationToken cancellationToken);
}

/// <summary>
/// One consumer middleware of the host, named by its type so the instance can
/// be resolved from the scope of each message.
/// </summary>
public sealed class ConsumerMiddlewareRegistration
{
    internal ConsumerMiddlewareRegistration(Type middlewareType) => MiddlewareType = middlewareType;

    /// <summary>Type implementing <see cref="IConsumerMiddleware"/>.</summary>
    public Type MiddlewareType { get; }
}

/// <summary>
/// Builds the consumers of a host over its shared broker connection and the
/// consumer pipeline the host declared.
/// </summary>
public interface IDmConsumerBuilder
{
    /// <summary>Builds a consumer for one queue.</summary>
    /// <param name="parameters">Topology of the queue.</param>
    IDmConsumer Build<TMessage, TProcessor>(DmConsumerParameters parameters)
        where TProcessor : IProcessor<string, TMessage>;
}

/// <inheritdoc />
internal sealed class DmConsumerBuilder(
    DmBrokerConnection connection,
    IServiceScopeFactory scopeFactory,
    IEnumerable<ConsumerMiddlewareRegistration> middlewares,
    ILoggerFactory loggerFactory) : IDmConsumerBuilder
{
    /// <inheritdoc />
    public IDmConsumer Build<TMessage, TProcessor>(DmConsumerParameters parameters)
        where TProcessor : IProcessor<string, TMessage> =>
        new DmConsumer<TMessage, TProcessor>(
            connection, scopeFactory, middlewares, parameters,
            loggerFactory.CreateLogger("DM.Infrastructure.Messaging.DmConsumer"));
}

/// <inheritdoc />
/// <remarks>
/// One scope per message. Failure, an escaped exception and an unreadable body are rejected without requeue.
/// </remarks>
internal sealed class DmConsumer<TMessage, TProcessor> : IDmConsumer
    where TProcessor : IProcessor<string, TMessage>
{
    private readonly DmBrokerConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Type[] _middlewares;
    private readonly DmConsumerParameters _parameters;
    private readonly ILogger _logger;

    internal DmConsumer(
        DmBrokerConnection connection,
        IServiceScopeFactory scopeFactory,
        IEnumerable<ConsumerMiddlewareRegistration> middlewares,
        DmConsumerParameters parameters,
        ILogger logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _middlewares = middlewares.Select(registration => registration.MiddlewareType).ToArray();
        _parameters = parameters;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Subscribe(CancellationToken cancellationToken)
    {
        var connection = await _connection.GetOpenConnection(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        try
        {
            // Prefetch of one: an unacknowledged backlog is invisible to messages_ready.
            // Never global: RabbitMQ 4.x refuses global=true.
            await channel.BasicQosAsync(0, 1, global: false, cancellationToken);

            // Matches every other declaration of the exchange, or the broker answers 406.
            await channel.ExchangeDeclareAsync(
                _parameters.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: cancellationToken);

            var queueName = _parameters.Exclusive
                ? $"{_parameters.QueueName}-{RandomSuffix()}"
                : _parameters.QueueName;

            // The routing key decides nothing behind a fanout, but a durable queue is
            // redeclared with the arguments it already has, or the broker answers 406.
            IDictionary<string, object?>? arguments = _parameters.DeadLetterExchange is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = _parameters.DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = _parameters.QueueName,
                };

            // Not auto-deleted: exclusivity already deletes the queue with the connection.
            await channel.QueueDeclareAsync(queueName,
                durable: !_parameters.Exclusive,
                exclusive: _parameters.Exclusive,
                autoDelete: false,
                arguments: arguments,
                cancellationToken: cancellationToken);

            foreach (var routingKey in _parameters.RoutingKeys)
            {
                await channel.QueueBindAsync(queueName, _parameters.ExchangeName, routingKey,
                    cancellationToken: cancellationToken);
            }

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (_, delivery) => Handle(channel, delivery);

            await channel.BasicConsumeAsync(queueName, autoAck: false, _parameters.ConsumerTag,
                noLocal: false, exclusive: false, arguments: null, consumer, cancellationToken);
        }
        catch
        {
            // Every subscription attempt opens its own channel.
            await channel.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Takes one delivery through decode, scope, pipeline and acknowledgement.
    /// </summary>
    /// <remarks>
    /// Nothing may escape: the client drops the exception, and the message stays unacknowledged.
    /// </remarks>
    private async Task Handle(IChannel channel, BasicDeliverEventArgs delivery)
    {
        var cancellationToken = delivery.CancellationToken;

        TMessage message;
        try
        {
            message = JsonSerializer.Deserialize<TMessage>(delivery.Body.Span, DmMessage.Codec)
                ?? throw new JsonException("The body decoded to null");
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception,
                "A message of {Queue} cannot be decoded as {MessageType} and is rejected",
                _parameters.QueueName, typeof(TMessage).Name);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false,
                cancellationToken);
            return;
        }

        try
        {
            var result = await ProcessInScope(delivery, message, cancellationToken);
            if (result == ProcessResult.Success)
            {
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken);
            }
            else
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false,
                    cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A stopping host is not a failing message: the closing connection requeues it.
            _logger.LogInformation(
                "A message of {Queue} was interrupted by shutdown and returns to the queue",
                _parameters.QueueName);
        }
        catch (Exception exception)
        {
            // The retries already ran inside the pipeline.
            _logger.LogError(exception,
                "A message of {Queue} left the pipeline as an exception and is rejected",
                _parameters.QueueName);
            try
            {
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false,
                    cancellationToken);
            }
            catch (Exception nackFailure)
            {
                // A channel closing mid-confirm; the closing connection requeues the message.
                _logger.LogWarning(nackFailure,
                    "Acknowledgement failed for {Queue}; the closing connection requeues the message",
                    _parameters.QueueName);
            }
        }
    }

    private async Task<ProcessResult> ProcessInScope(
        BasicDeliverEventArgs delivery, TMessage message, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        ConsumerDelegate pipeline = token => scope.ServiceProvider
            .GetRequiredService<TProcessor>()
            .Process(delivery.RoutingKey, message, token);

        // Wrapped back to front so the first declared middleware is the
        // outermost, the way a request pipeline reads.
        foreach (var middlewareType in _middlewares.Reverse())
        {
            var middleware = (IConsumerMiddleware)scope.ServiceProvider.GetRequiredService(middlewareType);
            var next = pipeline;
            pipeline = token => middleware.InvokeAsync(next, token);
        }

        return await pipeline(cancellationToken);
    }

    private const string SuffixAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Six random alphanumerics (dm.notifications.api-9iugW5); the alert selects the queue by prefix.
    /// </summary>
    private static string RandomSuffix() => string.Create(6, 0,
        (suffix, _) =>
        {
            for (var i = 0; i < suffix.Length; i++)
            {
                suffix[i] = SuffixAlphabet[Random.Shared.Next(SuffixAlphabet.Length)];
            }
        });
}
