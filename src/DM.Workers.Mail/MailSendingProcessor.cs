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

    /// <summary>
    /// The session this processor sends its letter through. Built here and opened
    /// on first use: a constructor cannot await, and opening it is several network
    /// round trips.
    /// </summary>
    private readonly SmtpClient _client = new();

    /// <inheritdoc />
    public MailSendingProcessor(
        IOptions<EmailConfiguration> configuration,
        ILogger<MailSendingProcessor> logger,
        ICorrelationTokenProvider correlationTokenProvider)
    {
        _logger = logger;
        _correlationTokenProvider = correlationTokenProvider;
        _configuration = configuration.Value;
    }

    /// <summary>
    /// The connected transport, opening the session on the first call.
    /// </summary>
    /// <remarks>
    /// Every step is the asynchronous overload. The handshake is several round
    /// trips - TCP, greeting, STARTTLS, greeting again, AUTH - and the blocking
    /// overloads spent all of them holding a thread pool thread inside a pipeline
    /// that is asynchronous everywhere else. The NoOp that used to close the
    /// sequence was one more round trip, asking a connection that had just
    /// finished answering whether it was there.
    ///
    /// A session per letter is deliberate and stays: a processor is resolved from
    /// its own scope for every delivered letter and closes what it opened, which
    /// is what MailTransportOwnershipShould holds.
    /// </remarks>
    private async Task<IMailTransport> ConnectedClient(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return _client;
        }

        await _client.ConnectAsync(_configuration.ServerHost, _configuration.ServerPort,
            TransportSecurity(), cancellationToken);
        await _client.AuthenticateAsync(_configuration.Username, _configuration.Password, cancellationToken);
        return _client;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> Process(string key, EmailLetter message, CancellationToken cancellationToken)
    {
        // Logged, not only written into the header: the identifier is the one
        // part of a letter that comes back — a bounce report, a relay's log and
        // the reader's own client all quote it — and without it here there was
        // nothing on this side to match any of them against.
        var messageId = MessageIdentifier.Build(
            _correlationTokenProvider.Current, _configuration.FromAddress);
        _logger.LogInformation("Sending letter {MessageId} to {Address}",
            messageId, message.Address.Obfuscate());

        var mimeMessage = new MimeMessage
        {
            From = { new MailboxAddress(_configuration.FromDisplayName, _configuration.FromAddress) },
            ReplyTo = { new MailboxAddress(_configuration.FromDisplayName, _configuration.ReplyToAddress) },
            To = { MailboxAddress.Parse(message.Address) },
            Subject = message.Subject,
            MessageId = messageId
        };

        mimeMessage.Body = BuildMessageBody(message);

        var client = await ConnectedClient(cancellationToken);
        await client.SendAsync(mimeMessage, cancellationToken);
        return ProcessResult.Success;
    }

    /// <summary>The submission port that is encrypted from the first byte.</summary>
    private const int ImplicitTlsPort = 465;

    /// <summary>
    /// How much of the encryption this deployment is willing to give up.
    /// </summary>
    /// <remarks>
    /// The mode used to be a constant written into the call, so a deployment that
    /// wanted encryption had no way to ask for it: an attacker on the path strips
    /// STARTTLS out of the greeting and the same session continues in the clear,
    /// carrying activation and password reset links. Opportunistic upgrade stays
    /// the default only because the local and preview stacks send through MailHog.
    /// </remarks>
    private SecureSocketOptions TransportSecurity()
    {
        if (!_configuration.RequireTls)
        {
            return SecureSocketOptions.StartTlsWhenAvailable;
        }

        // Port 465 is encrypted from the first byte: there is no plaintext greeting
        // to upgrade, and demanding STARTTLS on it fails a session that is already
        // encrypted.
        return _configuration.ServerPort == ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
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
        try
        {
            // A processor whose letter never reached the relay opened nothing, and
            // now that the transport is built rather than deferred, IsConnected is
            // what says so
            if (_client.IsConnected)
            {
                // Quit rather than drop: a socket closed without QUIT leaves the
                // relay counting the session until its own idle timeout expires
                await _client.DisconnectAsync(true, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            // Best effort: the letter is already sent, and throwing from disposal
            // would fail a message the consumer would then redeliver
            _logger.LogWarning(e, "Could not close the SMTP session cleanly");
        }

        _client.Dispose();
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
        try
        {
            if (_client.IsConnected)
            {
                _client.Disconnect(true, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Could not close the SMTP session cleanly");
        }

        _client.Dispose();
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
