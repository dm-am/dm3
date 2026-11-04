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
/// One codec for every producer and consumer: a message decoded under another convention fails silently.
/// </remarks>
internal static class DmMessage
{
    /// <summary>Serializer options of every published and consumed message.</summary>
    internal static readonly JsonSerializerOptions Codec = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The properties every published message carries.
    /// </summary>
    /// <remarks>
    /// Persistent always: a transient message in a durable queue does not survive a broker restart.
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
    /// Null publishes without confirmation: a refused event must not fail a request whose work is done.
    /// </remarks>
    public TimeSpan? PublishConfirmTimeout { get; init; }
}

/// <summary>
/// Publisher of one message type into one exchange.
/// </summary>
/// <remarks>
/// Owned by whoever built it: the channel opens on the first send and closes only in Dispose.
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
/// Sends are serialized: an AMQP channel is not safe to publish on from several threads.
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
            // Not ??=: a channel closed by its own protocol error (406/404) stays dead.
            if (_channel is not { IsOpen: true })
            {
                _channel = await OpenChannel(cancellationToken);
            }

            if (parameters.PublishConfirmTimeout is { } confirmTimeout)
            {
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

        // Confirmation tracking makes BasicPublishAsync await the broker, not the socket write.
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
