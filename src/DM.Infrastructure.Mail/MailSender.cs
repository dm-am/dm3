using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Messaging;
using FluentValidation;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;
using Microsoft.Extensions.Options;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Sends emails through the RabbitMQ queue the mail worker consumes.
/// </summary>
internal class MailSender : IMailSender, IDisposable
{
    private readonly IValidator<EmailLetter> validator;
    private readonly IProducer<string, EmailLetter> producer;

    /// <inheritdoc />
    public MailSender(
        IValidator<EmailLetter> validator,
        IProducerBuilder producerBuilder,
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
        producer = producerBuilder.BuildRabbit<EmailLetter>(
            new RabbitProducerParameters(MailTransport.ExchangeName)
            {
                PublishingTimeout = brokerConfiguration.Value.PublishConfirmTimeout,
            });
    }

    /// <inheritdoc />
    public async Task SendAsync(EmailLetter letter)
    {
        await validator.ValidateAndThrowAsync(letter);
        await producer.Send(string.Empty, letter, CancellationToken.None);
    }

    /// <summary>
    /// Returns the AMQP channel this sender took from the pool. Same reasoning as
    /// InvokedEventProducer in the messaging assembly: the producer leases a
    /// channel on its first send and gives it back only on Dispose, and the
    /// wrapper was neither disposable nor scoped.
    /// </summary>
    public void Dispose() => (producer as IDisposable)?.Dispose();
}
