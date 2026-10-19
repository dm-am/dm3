using System.Collections.Concurrent;
using DM.Domain.Core.Mail;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The letters the host sent, kept where a test can read them.
/// </summary>
/// <remarks>
/// The mail worker does not run here, so the broker drops every letter. A decorator: the real sender still
/// validates and waits for the confirm. Static, as <see cref="InMemoryObjectStorage" /> is.
/// </remarks>
public sealed class RecordingMailSender : IMailSender, IDisposable
{
    private static readonly ConcurrentQueue<EmailLetter> Letters = new();

    private readonly IMailSender _inner;

    public RecordingMailSender(IMailSender inner) => _inner = inner;

    /// <summary>What was sent to one address, in the order it was sent.</summary>
    /// <remarks>By address: the suite shares one host.</remarks>
    public static IReadOnlyList<EmailLetter> To(string email) => Letters
        .Where(letter => string.Equals(letter.Address, email, StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <inheritdoc />
    public async Task SendAsync(EmailLetter letter)
    {
        // Recorded after the send and not before: a letter the broker refused did
        // not leave, and counting it here would prove the opposite of what the
        // assertion above it reads as.
        await _inner.SendAsync(letter);
        Letters.Enqueue(letter);
    }

    /// <summary>
    /// Closes the sender underneath, which nothing else will.
    /// </summary>
    /// <remarks>Built by hand, so the container disposes this wrapper alone.</remarks>
    public void Dispose() => (_inner as IDisposable)?.Dispose();
}
