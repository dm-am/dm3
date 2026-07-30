using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Mail.Configuration;
using Jamq.Client.Abstractions.Consuming;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;
using DM.Domain.Core.Mail;

namespace DM.Workers.Mail;

/// <inheritdoc />
internal class MailSendingProcessor : IProcessor<string, EmailLetter>
{
    private readonly ILogger<MailSendingProcessor> _logger;
    private readonly ICorrelationTokenProvider _correlationTokenProvider;
    private readonly EmailConfiguration _configuration;
    private readonly Lazy<IMailTransport> _client;

    /// <inheritdoc />
    public MailSendingProcessor(
        IOptions<EmailConfiguration> configuration,
        ILogger<MailSendingProcessor> logger,
        ICorrelationTokenProvider correlationTokenProvider)
    {
        _logger = logger;
        _correlationTokenProvider = correlationTokenProvider;
        _configuration = configuration.Value;
        _client = new Lazy<IMailTransport>(() =>
        {
            var smtpClient = new SmtpClient();
            smtpClient.Connect(_configuration.ServerHost, _configuration.ServerPort,
                SecureSocketOptions.StartTlsWhenAvailable);
            smtpClient.Authenticate(_configuration.Username, _configuration.Password);
            smtpClient.NoOp();
            return smtpClient;
        });
    }

    /// <inheritdoc />
    public async Task<ProcessResult> Process(string key, EmailLetter message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending letter to {Address}", message.Address.Obfuscate());

        var mimeMessage = new MimeMessage
        {
            From = { new MailboxAddress(_configuration.FromDisplayName, _configuration.FromAddress) },
            ReplyTo = { new MailboxAddress(_configuration.FromDisplayName, _configuration.ReplyToAddress) },
            To = { MailboxAddress.Parse(message.Address) },
            Subject = message.Subject,
            MessageId = _correlationTokenProvider.Current.ToString()
        };

        mimeMessage.Body = BuildMessageBody(message);

        await _client.Value.SendAsync(mimeMessage, cancellationToken);
        return ProcessResult.Success;
    }

    private static MimeEntity BuildMessageBody(EmailLetter message)
    {
        var htmlPart = new TextPart(TextFormat.Html) { Text = message.Body };

        // If no linked resources, return simple HTML body
        if (message.LinkedResources.Count == 0)
        {
            return htmlPart;
        }

        // Build multipart/related for inline images (CID attachments)
        var multipart = new MultipartRelated { htmlPart };

        foreach (var resource in message.LinkedResources)
        {
            var attachment = new MimePart(resource.MimeType)
            {
                Content = new MimeContent(new MemoryStream(resource.Content)),
                ContentId = resource.ContentId,
                ContentDisposition = new ContentDisposition(ContentDisposition.Inline),
                ContentTransferEncoding = ContentEncoding.Base64
            };
            multipart.Add(attachment);
        }

        return multipart;
    }
}