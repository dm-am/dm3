using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Messaging;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Sends emails through the RabbitMQ queue the mail worker consumes.
/// </summary>
/// <remarks>
/// A confirm timeout means the confirmation did not arrive, not that the
/// letter did not: the broker may accept the publish a moment later, and a
/// visitor who retries the failed request may then receive the mail twice.
/// A duplicate is the accepted cost - for activation and password reset, a
/// lost letter is strictly worse than a doubled one.
/// </remarks>
internal class MailSender : IMailSender, IDisposable
{
    private readonly IValidator<EmailLetter> validator;
    private readonly IDmProducer<EmailLetter> producer;

    /// <inheritdoc />
    public MailSender(
        IValidator<EmailLetter> validator,
        IDmProducerBuilder producerBuilder,
        IOptions<RabbitMqConfiguration> brokerConfiguration)
    {
        this.validator = validator;

        // The one producer that waits to be told the broker took the message.
        //
        // Asking for it is what makes Send fail when the broker refuses or is
        // gone, and this is the only route where that failure has to reach the
        // caller: the letter is the whole of what a registration or a password
        // reset owes the reader, and it is not recoverable from anything left
        // behind. Without the wait the account was written, the request answered
        // 200, the letter went nowhere, and the reader waited for an activation
        // that was never queued.
        //
        // Not asked for on the event bus or the realtime push, deliberately. An
        // event is not the carrier of a fact (SYSTEM.md) - the write it reports
        // is already committed - so turning a refusal there into a 500 would fail
        // requests whose work is done, and cost the caller a retry that writes
        // everything a second time.
        producer = producerBuilder.Build<EmailLetter>(
            new DmProducerParameters(MailTransport.ExchangeName)
            {
                PublishConfirmTimeout = brokerConfiguration.Value.PublishConfirmTimeout,
            });
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailLetter letter)
    {
        await validator.ValidateAndThrowAsync(letter);
        await producer.Send(string.Empty, letter, CancellationToken.None);
    }

    /// <summary>
    /// Closes the AMQP channel this sender opened: the producer opens a channel
    /// on its first send and closes it only on Dispose, and the wrapper was
    /// once neither disposable nor scoped - one leaked channel per resolution
    /// until the broker's ceiling stopped publishing altogether.
    /// </summary>
    public void Dispose() => producer.Dispose();
}
