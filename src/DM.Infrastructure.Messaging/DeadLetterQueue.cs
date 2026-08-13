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
    /// Called before the consumer subscribes, and knowingly doubling the client:
    /// naming a dead-letter exchange in the consumer parameters already has the
    /// client declare this same topology. The double stands because that is an
    /// internal of a pinned version, and losing it fails in silence - an exchange
    /// nothing declared discards what is dead-lettered into it exactly as quietly
    /// as having no dead-letter exchange at all.
    ///
    /// The price of the double is agreement argument for argument: fanout,
    /// durable, not auto-deleted; the queue durable, not exclusive, not
    /// auto-deleted; an empty binding key. A declaration that disagrees is
    /// answered with 406 when the consumer subscribes, and the host stops.
    ///
    /// The queue is terminal on purpose. A TTL on it plus a dead-letter exchange
    /// pointing back at the working one is an unbounded retry loop with no attempt
    /// ceiling: the same unprocessable message returns every minute forever. A
    /// poison message has to stop somewhere a human can look at it.
    /// </remarks>
    /// <param name="connectionFactory">Broker connection factory.</param>
    /// <param name="exchangeName">Name of the dead-letter exchange to declare.</param>
    public static void DeclareTerminal(IAsyncConnectionFactory connectionFactory, string exchangeName)
    {
        var queueName = $"{exchangeName}-dlq";

        using var configuringConnection = connectionFactory.CreateConnection();
        using var channel = configuringConnection.CreateModel();

        channel.ExchangeDeclare(exchangeName, ExchangeType.Fanout, true);
        channel.QueueDeclare(queueName, true, false, false);
        channel.QueueBind(queueName, exchangeName, string.Empty);
    }
}
