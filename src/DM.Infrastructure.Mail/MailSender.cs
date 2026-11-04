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
/// A confirm timeout may still deliver the letter, so a retried request can mail twice: better than losing it.
/// </remarks>
internal class MailSender : IMailSender, IDisposable
{
    private readonly IValidator<EmailLetter> validator;
    private readonly IDmProducer<EmailLetter> producer;

    public MailSender(
        IValidator<EmailLetter> validator,
        IDmProducerBuilder producerBuilder,
        IOptions<RabbitMqConfiguration> brokerConfiguration)
    {
        this.validator = validator;

        // Waits for the broker's confirm: the letter is not recoverable from anything else.
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
    /// Closes the AMQP channel this sender opened.
    /// </summary>
    public void Dispose() => producer.Dispose();
}
