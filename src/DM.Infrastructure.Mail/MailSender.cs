using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Mail;
using FluentValidation;
using Jamq.Client.Abstractions.Producing;
using Jamq.Client.Rabbit.Producing;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Mail sender implementation that sends emails through RabbitMQ queue.
/// Implements both <see cref="DM.Domain.Core.Mail.IMailSender"/> and legacy <see cref="IMailSender"/>.
/// </summary>
internal class MailSender : DM.Domain.Core.Mail.IMailSender, IMailSender
{
    private readonly IValidator<MailLetter> validator;
    private readonly IProducer<string, MailLetter> producer;

    /// <inheritdoc />
    public MailSender(
        IValidator<MailLetter> validator,
        IProducerBuilder producerBuilder)
    {
        this.validator = validator;
        producer = producerBuilder.BuildRabbit<MailLetter>(new RabbitProducerParameters("dm.mail.sending"));
    }

    /// <summary>
    /// Sends an email using EmailLetter DTO from Domain.Core.
    /// Maps to internal MailLetter for queue processing.
    /// </summary>
    async Task DM.Domain.Core.Mail.IMailSender.SendAsync(EmailLetter letter)
    {
        var mailLetter = new MailLetter
        {
            Address = letter.Address,
            Subject = letter.Subject,
            Body = letter.Body,
            LinkedResources = letter.LinkedResources
                .Select(r => new LinkedResource
                {
                    ContentId = r.ContentId,
                    MimeType = r.MimeType,
                    Content = r.Content
                })
                .ToList()
        };

        await SendAsync(mailLetter);
    }

    /// <inheritdoc />
    public async Task SendAsync(MailLetter letter)
    {
        await validator.ValidateAndThrowAsync(letter);
        await producer.Send(string.Empty, letter, CancellationToken.None);
    }
}
