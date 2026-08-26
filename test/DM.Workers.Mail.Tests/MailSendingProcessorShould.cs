using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Mail.Configuration;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using Xunit;
using DM.Workers.Mail.Sending;

namespace DM.Workers.Mail.Tests;

/// <summary>
/// What the worker writes about a letter is not the letter.
/// </summary>
/// <remarks>
/// The log of this host is shipped off the machine, and a letter has three things in
/// it that do not belong there: the body, which carries the activation and password
/// reset links; the address it goes to, which is a reader's; and the credentials the
/// session to the relay is opened with. The one line written per letter is where all
/// three are within reach at once, which is why it is asserted about rather than
/// read.
///
/// Nothing here opens a socket. The relay host is left empty on purpose: MailKit
/// refuses a zero-length host by argument check, before any name resolution and any
/// connection, and everything asserted below is written before that call. A test that
/// pointed at a port instead would be asserting about the network of whichever
/// machine happened to run it.
/// </remarks>
public class MailSendingProcessorShould
{
    private const string Recipient = "reader@example.com";
    private const string Subject = "Registration confirmation";
    private const string ActivationLink = "https://dm.am/account/activate?token=0123456789abcdef";
    private const string RelayPassword = "s3cret-relay-password";

    private readonly RecordingLogger<MailSendingProcessor> _logger = new();

    /// <summary>
    /// Both shapes of letter, because the body is built one way with inline images and
    /// another without, and the line is written before either.
    /// </summary>
    /// <param name="withInlineImages">Whether the letter carries a linked resource.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task KeepTheBodyTheAddressAndTheCredentialsOutOfTheLog(bool withInlineImages)
    {
        await using var processor = Processor();

        var send = () => processor.Process("key", Letter(withInlineImages), CancellationToken.None);

        await send.Should().ThrowAsync<ArgumentException>(
            "an empty relay host is refused by argument check, which is what stands in for a " +
            "relay here");

        var written = string.Join(Environment.NewLine, _logger.Messages);

        written.Should().NotBeEmpty(
            "the send writes a line about the letter, and an empty log would pass everything below");
        written.Should().Contain(Recipient.Obfuscate(),
            "the line has to say which letter it is about, and the obfuscated address is what " +
            "says it");
        written.Should().NotContain(Recipient,
            "an address written out in full is a reader's, and it stays there for the retention " +
            "of the log");
        written.Should().NotContain(ActivationLink,
            "the body carries the links that let whoever reads them take the account");
        written.Should().NotContain(RelayPassword,
            "the credentials of the relay are what everything this host sends goes out under");
    }

    private MailSendingProcessor Processor() => new(
        Options.Create(new EmailConfiguration
        {
            // Empty on purpose: this is what stops the send at an argument check
            // instead of at a socket.
            ServerHost = string.Empty,
            ServerPort = 587,
            Username = "relay-user",
            Password = RelayPassword,
            FromAddress = "noreply@dm.am",
            FromDisplayName = "DM",
            ReplyToAddress = "noreply@dm.am"
        }),
        _logger,
        new FixedCorrelationToken());

    private static EmailLetter Letter(bool withInlineImages)
    {
        var letter = new EmailLetter
        {
            Address = Recipient,
            Subject = Subject,
            Body = $"<p>Welcome. <a href=\"{ActivationLink}\">Confirm</a></p>"
        };

        if (withInlineImages)
        {
            letter.LinkedResources =
            [
                new LinkedResource
                {
                    ContentId = "logo",
                    MimeType = "image/png",
                    Content = [0x89, 0x50, 0x4E, 0x47]
                }
            ];
        }

        return letter;
    }

    /// <summary>The token the request that asked for the letter would have carried.</summary>
    private sealed class FixedCorrelationToken : ICorrelationTokenProvider
    {
        public Guid Current { get; } = Guid.NewGuid();
    }
}
