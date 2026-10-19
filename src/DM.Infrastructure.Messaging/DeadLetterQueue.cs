using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Terminal destination for messages a consumer could not process.
/// </summary>
/// <remarks>
/// One place, because the naming and the terminality have to agree between consumers.
/// </remarks>
public static class DeadLetterQueue
{
    /// <summary>
    /// Declares a dead-letter exchange together with the queue that keeps whatever
    /// lands in it.
    /// </summary>
    /// <remarks>
    /// <see cref="DmConsumerParameters.DeadLetterExchange"/> routes into the exchange, this makes it exist.
    /// Declared exactly as the broker already holds it (fanout, durable, not auto-deleted, empty binding key),
    /// or it answers 406 at subscribe and the host stops.
    /// Terminal: a TTL pointing back at the working queue would retry a poison message forever.
    /// </remarks>
    /// <param name="connection">Broker connection of the host.</param>
    /// <param name="exchangeName">Name of the dead-letter exchange to declare.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task DeclareTerminal(
        DmBrokerConnection connection, string exchangeName, CancellationToken cancellationToken)
    {
        var queueName = $"{exchangeName}-dlq";

        var open = await connection.GetOpenConnection(cancellationToken);
        await using var channel = await open.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true,
            autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queueName, exchangeName, string.Empty,
            cancellationToken: cancellationToken);
    }
}
