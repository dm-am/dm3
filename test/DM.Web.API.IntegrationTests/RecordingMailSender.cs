using System.Collections.Concurrent;
using DM.Domain.Core.Mail;

namespace DM.Web.API.IntegrationTests;

/// <remarks>
/// The mail worker does not run here, so every letter stays a row of the mail queue. A decorator: the real
/// sender still validates and stores the row. Static, as <see cref="InMemoryObjectStorage" /> is.
/// </remarks>
public sealed class RecordingMailSender : IMailSender, IDisposable
{
    private static readonly ConcurrentQueue<EmailLetter> Letters = new();

    private readonly IMailSender _inner;

    public RecordingMailSender(IMailSender inner) => _inner = inner;

    // By address: the suite shares one host.
    public static IReadOnlyList<EmailLetter> To(string email) => Letters
        .Where(letter => string.Equals(letter.Address, email, StringComparison.OrdinalIgnoreCase))
        .ToList();

    /// <inheritdoc />
    public async Task SendAsync(EmailLetter letter)
    {
        // Recorded after the send and not before: a letter the queue refused was
        // never stored, and counting it here would prove the opposite of what the
        // assertion above it reads as.
        await _inner.SendAsync(letter);
        Letters.Enqueue(letter);
    }

    // Built by hand, so the container disposes this wrapper alone.
    public void Dispose() => (_inner as IDisposable)?.Dispose();
}
