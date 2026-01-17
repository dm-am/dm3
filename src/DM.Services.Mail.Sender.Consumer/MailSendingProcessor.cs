using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.Core.Configuration;
using DM.Services.Core.Implementation.CorrelationToken;
using Jamq.Client.Abstractions.Consuming;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MimeKit.Text;

namespace DM.Services.Mail.Sender.Consumer;

/// <inheritdoc />
internal class MailSendingProcessor : IProcessor<string, MailLetter>
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
    public async Task<ProcessResult> Process(string key, MailLetter message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sending letter to {Address}", message.Address.Obfuscate());
        await _client.Value.SendAsync(new MimeMessage
        {
            From = {new MailboxAddress(_configuration.FromDisplayName, _configuration.FromAddress)},
            ReplyTo = {new MailboxAddress(_configuration.FromDisplayName, _configuration.ReplyToAddress)},
            To = {MailboxAddress.Parse(message.Address)},
            Subject = message.Subject,
            Body = new TextPart(TextFormat.Html) {Text = message.Body},
            MessageId = _correlationTokenProvider.Current.ToString()
        }, cancellationToken);
        return ProcessResult.Success;
    }
}