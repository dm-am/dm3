using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Terminal destination for messages a consumer could not process.
/// </summary>
/// <remarks>
/// When the retries run out the exception escapes the pipeline and the message is
/// rejected without requeue. A queue with no dead-letter exchange has it dropped
/// there and then: nothing is stored, nothing is left past the warnings of the
/// retry middleware, and the event cannot be reconstructed from anywhere. So every
/// queue that carries work somebody is waiting for names one of these.
///
/// One place rather than one per consumer, because the naming and the terminality
/// are exactly the parts that have to agree between them.
/// </remarks>
public static class DeadLetterQueue
{
    /// <summary>
    /// Declares a dead-letter exchange together with the queue that keeps whatever
    /// lands in it.
    /// </summary>
    /// <remarks>
    /// The other half of the routing lives in the consumer parameters: naming a
    /// <see cref="DmConsumerParameters.DeadLetterExchange"/> stamps the argument
    /// on the working queue, and this call is what makes the named exchange
    /// exist and keep what arrives. Losing either half fails in silence — an
    /// exchange nothing declared discards what is dead-lettered into it exactly
    /// as quietly as having no dead-letter exchange at all.
    ///
    /// The two declarations have to agree argument for argument: fanout,
    /// durable, not auto-deleted; the queue durable, not exclusive, not
    /// auto-deleted; an empty binding key. A declaration that disagrees is
    /// answered with 406 when the consumer subscribes, and the host stops.
    ///
    /// The queue is terminal on purpose. A TTL on it plus a dead-letter exchange
    /// pointing back at the working one is an unbounded retry loop with no attempt
    /// ceiling: the same unprocessable message returns every minute forever. A
    /// poison message has to stop somewhere a human can look at it.
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
