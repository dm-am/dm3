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
internal class MailSendingProcessor : IProcessor<string, EmailLetter>, IDisposable, IAsyncDisposable
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

    /// <summary>
    /// Closes the SMTP session this processor opened, if it opened one.
    /// </summary>
    /// <remarks>
    /// A processor is resolved from a fresh scope for every delivered letter, and
    /// a scope releases only what it can see as disposable. Without this the
    /// session stays connected and authenticated until a finalizer happens to
    /// reach the socket: relays cap concurrent connections per address, and past
    /// the cap every further letter is retried, dead-lettered and never delivered
    /// — activation and password reset mail included.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        // A lazy factory that threw leaves IsValueCreated false: nothing was opened
        if (!_client.IsValueCreated)
        {
            return;
        }

        var client = _client.Value;
        try
        {
            if (client.IsConnected)
            {
                // Quit rather than drop: a socket closed without QUIT leaves the
                // relay counting the session until its own idle timeout expires
                await client.DisconnectAsync(true, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            // Best effort: the letter is already sent, and throwing from disposal
            // would fail a message the consumer would then redeliver
            _logger.LogWarning(e, "Could not close the SMTP session cleanly");
        }

        client.Dispose();
    }

    /// <summary>
    /// Synchronous fallback, for a scope that is disposed synchronously.
    /// </summary>
    /// <remarks>
    /// Both paths exist because the container picks one by how the scope is
    /// closed, and a type that is only IAsyncDisposable gets disposed
    /// sync-over-async — on a network round trip that is worth avoiding.
    /// </remarks>
    public void Dispose()
    {
        if (!_client.IsValueCreated)
        {
            return;
        }

        var client = _client.Value;
        try
        {
            if (client.IsConnected)
            {
                client.Disconnect(true, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not close the SMTP session cleanly");
        }

        client.Dispose();
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