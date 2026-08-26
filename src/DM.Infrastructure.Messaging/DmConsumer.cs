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
/// <remarks>
/// Written out in the consumer's own file, which is the point of carrying a
/// parameters object at all: the binding, the dead-letter exchange and the
/// exclusivity are decisions about where work survives a failure, and they are
/// read next to the code that takes the work.
/// </remarks>
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
    /// Exchange a message this consumer could not process is rejected into.
    /// Null drops such messages at the broker, which is a decision and not a
    /// default worth having: DeadLetterRoutingShould names the one queue that
    /// may.
    /// </summary>
    public string? DeadLetterExchange { get; init; }

    /// <summary>
    /// Declares the queue exclusive to this connection, under a name with a
    /// random suffix, deleted when the connection goes.
    /// </summary>
    /// <remarks>
    /// The suffix keeps two instances of a host from stepping on one queue: an
    /// exclusive declaration under a fixed name refuses the second subscriber
    /// outright. The broker therefore reports such a queue under the configured
    /// name plus the suffix — which is why the alert about the realtime push
    /// queue matches it by prefix, and why the suffix is part of the observable
    /// contract rather than an internal.
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
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Subscribe(CancellationToken cancellationToken);
}

/// <summary>
/// One consumer middleware of the host, named by its type so the instance can
/// be resolved from the scope of each message.
/// </summary>
/// <remarks>
/// The constructor is internal so the blanket assembly scan over this assembly
/// leaves the type alone: the scan registers whatever declares a public one,
/// and a scanned registration of this class would join the IEnumerable the
/// consumer builder takes and fail activation on the Type argument nothing in
/// the graph answers for. The one way to an instance is
/// MessagingConfigurationExtensions.AddDmConsumerMiddleware, where a host
/// declares its pipeline.
/// </remarks>
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
/// One scope per delivered message, opened around the pipeline and released
/// after the acknowledgement is decided: the processor and the middlewares are
/// resolved from it, so their dependencies — one DmDbContext per scope among
/// them — live exactly as long as the processing does.
///
/// Acknowledgement follows <see cref="ProcessResult"/>: Success is acked,
/// Failure and any exception that escapes the pipeline are rejected without
/// requeue, which hands the message to the dead-letter exchange where the
/// queue names one. A body the codec cannot read is rejected the same way
/// without entering the pipeline: it will not read better on any attempt, and
/// replaying it would spend the whole retry ladder saying so.
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
            // Prefetch of exactly one, never global. Without the limit the
            // broker hands over the whole queue at once and the worker holds
            // every message of it in memory, unacknowledged — a backlog no rule
            // can see, because depth is read off messages_ready and a message
            // already handed to a consumer is not ready any more. It buys no
            // parallelism to pay for that: the handler runs on the client's
            // dispatcher, one message at a time on the channel either way. And
            // global=false is load-bearing on its own — global=true is refused
            // by RabbitMQ 4.x, which is the call that kept the previous client
            // wrapper off that broker entirely.
            await channel.BasicQosAsync(0, 1, global: false, cancellationToken);

            // Declared to match, argument for argument, what every other end of
            // this topology declares: a durable topic exchange that is not
            // auto-deleted. A declaration that disagrees is answered with 406.
            await channel.ExchangeDeclareAsync(
                _parameters.ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false,
                cancellationToken: cancellationToken);

            var queueName = _parameters.Exclusive
                ? $"{_parameters.QueueName}-{RandomSuffix()}"
                : _parameters.QueueName;

            // The routing key argument pins what a dead-lettered message is
            // republished under to the queue's own name. The dead-letter
            // exchanges are fanout, so it decides nothing about delivery -
            // but it is what the previous client stamped on every working
            // queue, and a durable queue is redeclared with exactly the
            // arguments it already has or the broker answers 406 and the
            // consumer never subscribes.
            IDictionary<string, object?>? arguments = _parameters.DeadLetterExchange is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = _parameters.DeadLetterExchange,
                    ["x-dead-letter-routing-key"] = _parameters.QueueName,
                };

            // The exclusive queue is not auto-deleted on top of being
            // exclusive: exclusivity already deletes it with the connection,
            // and this is the shape the broker holds from the previous client.
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
            // The subscription is retried by the host's policy, and each attempt
            // opens a channel of its own; one that failed partway must not sit
            // open until the process ends.
            await channel.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Takes one delivery through decode, scope, pipeline and acknowledgement.
    /// </summary>
    /// <remarks>
    /// Nothing may escape: the client's dispatcher routes an exception from
    /// this handler into a callback event nobody observes, so an unhandled
    /// throw here is a message that is neither acked nor rejected — parked
    /// against the prefetch window until the connection closes.
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
            // A stopping host is not a failing message: no nack, no dead letter.
            // Leaving the delivery unacknowledged is the hand-back - the closing
            // connection requeues it for the next start (review of W1.2).
            _logger.LogInformation(
                "A message of {Queue} was interrupted by shutdown and returns to the queue",
                _parameters.QueueName);
        }
        catch (Exception exception)
        {
            // The retries have already happened inside the pipeline where the
            // queue installs them; an exception here is a message that
            // exhausted them, and rejecting without requeue is what hands it
            // to the dead-letter exchange rather than back to the queue.
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
                // The remarks above promise that nothing escapes this handler,
                // and the acknowledgement itself is the last thing able to
                // throw (a channel closing mid-confirm). The closing
                // connection requeues what was not confirmed.
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

        var context = new ConsumerContext(delivery.RoutingKey, delivery.Redelivered);

        ConsumerDelegate pipeline = (_, token) => scope.ServiceProvider
            .GetRequiredService<TProcessor>()
            .Process(delivery.RoutingKey, message, token);

        // Wrapped back to front so the first declared middleware is the
        // outermost, the way a request pipeline reads.
        foreach (var middlewareType in _middlewares.Reverse())
        {
            var middleware = (IConsumerMiddleware)scope.ServiceProvider.GetRequiredService(middlewareType);
            var next = pipeline;
            pipeline = (consumerContext, token) => middleware.InvokeAsync(consumerContext, next, token);
        }

        return await pipeline(context, cancellationToken);
    }

    private const string SuffixAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>
    /// Six random alphanumerics, the shape the exclusive queue names have
    /// always had on the broker (dm.notifications.api-9iugW5). The prometheus
    /// alert selects the queue by prefix because of exactly this suffix, so
    /// its shape is kept rather than reinvented.
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
