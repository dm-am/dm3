using System.Collections.Concurrent;
using DM.Domain.Core.Mail;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The letters the host sent, kept where a test can read them.
/// </summary>
/// <remarks>
/// A letter leaves through a fanout exchange the mail worker consumes, and that
/// worker is a separate process which does not run here. Nothing is bound to the
/// exchange inside the suite, so the broker drops every message: a decision that
/// mails its owner and one that silently does not looked exactly alike from in
/// here, and there was no surface on which to assert the difference.
///
/// A decorator and not a substitute. The real sender stays underneath and still
/// validates the letter and waits for the broker to confirm it, so the half of
/// the path the suite already exercised is exercised still; what is added is the
/// copy kept here.
///
/// Static, for the reason <see cref="InMemoryObjectStorage" /> is static: the
/// host is shared across the whole collection, and a test has no handle on the
/// instance that served its request.
/// </remarks>
public sealed class RecordingMailSender : IMailSender, IDisposable
{
    private static readonly ConcurrentQueue<EmailLetter> Letters = new();

    private readonly IMailSender _inner;

    public RecordingMailSender(IMailSender inner) => _inner = inner;

    /// <summary>What was sent to one address, in the order it was sent.</summary>
    /// <remarks>
    /// By address rather than by the whole log, because the suite shares one host:
    /// a registration in the next test over puts its own letter in this queue, and
    /// an assertion over everything would be answering about somebody else's mail.
    /// </remarks>
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
    /// <remarks>
    /// It holds an open channel and is built by hand rather than resolved, so the
    /// container never learned about it and disposes this wrapper alone.
    /// </remarks>
    public void Dispose() => (_inner as IDisposable)?.Dispose();
}
