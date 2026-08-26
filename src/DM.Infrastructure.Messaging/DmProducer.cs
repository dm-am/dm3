using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// The wire format every message of this system travels in.
/// </summary>
/// <remarks>
/// One codec and one properties factory for the whole system, because the two
/// halves of the contract have to agree between every producer and every
/// consumer: a message encoded under one convention and decoded under another
/// fails on the field that differs and nowhere else. Web defaults — camelCase
/// written, case-insensitive read — so the payload reads the same as the HTTP
/// API of this system and a message queued under the previous casing still
/// decodes.
/// </remarks>
internal static class DmMessage
{
    /// <summary>Serializer options of every published and consumed message.</summary>
    internal static readonly JsonSerializerOptions Codec = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The properties every published message carries.
    /// </summary>
    /// <remarks>
    /// Persistent unconditionally. Every queue holding work is declared durable,
    /// and a non-persistent message is held by the broker in memory only, so a
    /// broker restart used to bring back the queues and none of their contents —
    /// including dm.mail.sending, where a letter is the only record that a
    /// registration confirmation is owed, and the dead-letter queue, which
    /// exists precisely so a human can look at what could not be sent. There is
    /// no exemption for the realtime push: a persistent message routed to a
    /// transient queue is not written to disk anyway, so an exemption would save
    /// nothing and would give the rule an exception to drift through.
    /// </remarks>
    internal static BasicProperties DeliveryProperties() => new BasicProperties
    {
        Persistent = true,
        ContentType = "application/json",
    };
}

/// <summary>
/// What one producer publishes to and whether it waits to be told the broker
/// took the message.
/// </summary>
/// <param name="exchangeName">Exchange this producer publishes to.</param>
public sealed class DmProducerParameters(string exchangeName)
{
    /// <summary>Exchange this producer publishes to.</summary>
    public string ExchangeName { get; } = exchangeName;

    /// <summary>
    /// How long a publish waits for the broker to confirm it took the message.
    /// </summary>
    /// <remarks>
    /// Null publishes without asking for confirmation, which is the default and
    /// the right answer inside a request: an event is not the carrier of the
    /// fact, and a refusal there must not fail a request whose work is done.
    /// Set where the message is the whole obligation (MailSender) and where a
    /// durable mark depends on the broker having taken the message (the outbox
    /// relay) — PublishedExchangeShould names both and nobody else.
    /// </remarks>
    public TimeSpan? PublishConfirmTimeout { get; init; }
}

/// <summary>
/// Publisher of one message type into one exchange.
/// </summary>
/// <remarks>
/// Owned by whoever built it: the producer opens an AMQP channel on its first
/// send and closes it only in Dispose, so a wrapper that cannot be disposed
/// leaks one channel per instance. The wrappers are registered per lifetime
/// scope — one channel per scope, returned when the scope ends — and
/// MessageProducerOwnershipShould holds the disposability half of that.
/// </remarks>
public interface IDmProducer<in TMessage> : IDisposable
{
    /// <summary>Publishes one message.</summary>
    /// <param name="routingKey">Routing key to publish under.</param>
    /// <param name="message">Message to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task Send(string routingKey, TMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Builds the producers of a host over its shared broker connection.
/// </summary>
public interface IDmProducerBuilder
{
    /// <summary>Builds a producer for one message type.</summary>
    /// <param name="parameters">What the producer publishes to and how.</param>
    IDmProducer<TMessage> Build<TMessage>(DmProducerParameters parameters);
}

/// <inheritdoc />
internal sealed class DmProducerBuilder(DmBrokerConnection connection) : IDmProducerBuilder
{
    /// <inheritdoc />
    public IDmProducer<TMessage> Build<TMessage>(DmProducerParameters parameters) =>
        new DmProducer<TMessage>(connection, parameters);
}

/// <inheritdoc />
/// <remarks>
/// The channel is opened lazily on the first send and every send is serialized
/// on a semaphore: an AMQP channel is not safe to publish on from several
/// threads at once, and the wrapper being scoped does not stop two tasks of one
/// request from publishing concurrently.
///
/// Where <see cref="DmProducerParameters.PublishConfirmTimeout"/> is set the
/// channel is opened with publisher confirmations and every publish awaits the
/// broker's confirm: a refusal surfaces as the client's own exception, and a
/// confirm that does not arrive within the timeout surfaces as
/// <see cref="TimeoutException"/>. Without the setting a publish only awaits
/// the socket write, which is the fire-and-forget the realtime push wants.
/// </remarks>
internal sealed class DmProducer<TMessage>(
    DmBrokerConnection connection,
    DmProducerParameters parameters) : IDmProducer<TMessage>
{
    private readonly SemaphoreSlim _publishing = new(1, 1);
    private IChannel? _channel;

    /// <inheritdoc />
    public async Task Send(string routingKey, TMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, DmMessage.Codec);
        var properties = DmMessage.DeliveryProperties();

        await _publishing.WaitAsync(cancellationToken);
        try
        {
            // Not ??=: automatic recovery revives channels lost with the
            // connection, but a channel closed by its own protocol error
            // (406/404) stays dead, and caching it would fail every later
            // Send of this scope (review of W1.2). One reopen attempt.
            if (_channel is not { IsOpen: true })
            {
                _channel = await OpenChannel(cancellationToken);
            }

            if (parameters.PublishConfirmTimeout is { } confirmTimeout)
            {
                // The ceiling is what a person will sit through rather than what
                // a broker might need: the wait happens inside a request the
                // reader is watching, and a broker that has not answered within
                // it is not about to.
                using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                confirmation.CancelAfter(confirmTimeout);
                try
                {
                    await _channel.BasicPublishAsync(
                        parameters.ExchangeName, routingKey, mandatory: false, properties, body,
                        confirmation.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"The broker did not confirm a publish to {parameters.ExchangeName} " +
                        $"within {confirmTimeout}");
                }
            }
            else
            {
                await _channel.BasicPublishAsync(
                    parameters.ExchangeName, routingKey, mandatory: false, properties, body,
                    cancellationToken);
            }
        }
        finally
        {
            _publishing.Release();
        }
    }

    private async Task<IChannel> OpenChannel(CancellationToken cancellationToken)
    {
        var open = await connection.GetOpenConnection(cancellationToken);

        // Confirmation tracking is what makes BasicPublishAsync await the
        // broker's answer instead of completing on the socket write. Asked for
        // only where a timeout is configured, because the wait is the price and
        // the event bus must not pay it.
        var options = parameters.PublishConfirmTimeout is null
            ? null
            : new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

        return await open.CreateChannelAsync(options, cancellationToken);
    }

    /// <summary>
    /// Closes the channel this producer opened, if it opened one.
    /// </summary>
    public void Dispose()
    {
        _channel?.Dispose();
        _channel = null;
        _publishing.Dispose();
    }
}
