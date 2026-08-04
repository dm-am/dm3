using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using FluentValidation;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

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
        IProducerBuilder producerBuilder)
    {
        this.validator = validator;
        producer = producerBuilder.BuildRabbit<EmailLetter>(new RabbitProducerParameters("dm.mail.sending"));
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
