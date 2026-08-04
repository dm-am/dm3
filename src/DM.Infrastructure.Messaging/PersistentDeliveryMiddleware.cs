using System.Threading;
using System.Threading.Tasks;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace DM.Infrastructure.Messaging;

/// <summary>
/// Marks every published message persistent.
/// </summary>
/// <remarks>
/// The client writes the body and leaves BasicProperties untouched, which leaves
/// delivery mode at 1: the broker holds such a message in memory only. Every
/// queue in the system is declared durable, so a restart of the broker used to
/// bring back the queues and none of their contents — including dm.mail.sending,
/// where a letter is the only record that a registration confirmation is owed,
/// and the dead-letter queue, which exists precisely so a human can look at what
/// could not be sent.
///
/// A producer middleware because it is the only hook there is: the properties
/// object is created inside Send and exposed nowhere else. Message-agnostic, so
/// one registration covers every message type in every host.
///
/// Unconditional, with no exemption for the realtime push: a persistent message
/// routed to a transient queue is not written to disk anyway, so the exemption
/// would save nothing and would give the rule an exception to drift through.
/// </remarks>
internal class PersistentDeliveryMiddleware : IProducerMiddleware<RabbitProducerProperties>
{
    /// <inheritdoc />
    public Task InvokeAsync(
        ProducerContext<RabbitProducerProperties> context,
        ProducerDelegate<RabbitProducerProperties> next,
        CancellationToken cancellationToken)
    {
        context.NativeProperties.BasicProperties.Persistent = true;
        return next(context, cancellationToken);
    }
}
