using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Mail;
using DM.Infrastructure.Mail.Configuration;
using DM.Infrastructure.Messaging;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using DM.Domain.Core.Mail;

namespace DM.Workers.Mail.Sending;

/// <inheritdoc />
internal class MailSendingProcessor : IProcessor<string, EmailLetter>, IDisposable, IAsyncDisposable
{
    private readonly ILogger<MailSendingProcessor> _logger;
    private readonly ICorrelationTokenProvider _correlationTokenProvider;
    private readonly EmailConfiguration _configuration;

    /// <summary>
    /// The session this processor sends its letter through, opened on first use.
    /// </summary>
    private readonly SmtpClient _client = new();

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
    /// A session per letter: the processor lives in the scope of one message and closes what it opened.
    /// </remarks>
    private async Task<IMailTransport> ConnectedClient(CancellationToken cancellationToken)
    {
        if (_client.IsConnected)
        {
            return _client;
        }

        await _client.ConnectAsync(_configuration.ServerHost, _configuration.ServerPort,
            TransportSecurity(), cancellationToken);

        // MailKit refuses to authenticate against a relay that advertises no AUTH.
        if (_configuration.UsesAuthentication)
        {
            await _client.AuthenticateAsync(_configuration.Username, _configuration.Password, cancellationToken);
        }

        return _client;
    }

    /// <inheritdoc />
    public async Task<ProcessResult> Process(string key, EmailLetter message, CancellationToken cancellationToken)
    {
        // Logged: a bounce report and a relay log quote the identifier back.
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

        mimeMessage.Body = LetterBody.Build(message);

        var client = await ConnectedClient(cancellationToken);
        await client.SendAsync(mimeMessage, cancellationToken);
        return ProcessResult.Success;
    }

    /// <summary>The submission port that is encrypted from the first byte.</summary>
    private const int ImplicitTlsPort = 465;

    /// <summary>
    /// How much of the encryption this deployment is willing to give up.
    /// </summary>
    private SecureSocketOptions TransportSecurity()
    {
        if (!_configuration.RequireTls)
        {
            return SecureSocketOptions.StartTlsWhenAvailable;
        }

        // Port 465 has no plaintext greeting to upgrade.
        return _configuration.ServerPort == ImplicitTlsPort
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
    }

    /// <summary>
    /// Closes the SMTP session this processor opened, if it opened one.
    /// </summary>
    /// <remarks>
    /// Relays cap concurrent connections per address, so a session must not wait for a finalizer.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
            {
                // Quit rather than drop: the relay counts a dropped session until its idle timeout
                await _client.DisconnectAsync(true, CancellationToken.None);
            }
        }
        catch (Exception e)
        {
            // The letter is already sent: throwing here would fail the message
            _logger.LogWarning(e, "Could not close the SMTP session cleanly");
        }

        _client.Dispose();
    }

    /// <summary>
    /// Synchronous fallback, for a scope that is disposed synchronously.
    /// </summary>
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
}
