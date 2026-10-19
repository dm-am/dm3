using System;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Message-Id of an outgoing letter.
/// </summary>
/// <remarks>
/// RFC 5322 id-left "@" id-right: a relay replaces a malformed value, and the bounce then quotes another id.
/// </remarks>
public static class MessageIdentifier
{
    /// <summary>
    /// Domain used when the configured sender address carries none, rather than a header ending in "@".
    /// </summary>
    private const string FallbackDomain = "localhost";

    /// <summary>
    /// Builds the Message-Id of one letter.
    /// </summary>
    /// <param name="token">Correlation token of the send, which becomes the left-hand side.</param>
    /// <param name="fromAddress">Address the letter is sent from, which supplies the domain.</param>
    /// <returns>A msg-id without the angle brackets, as the mail library takes it.</returns>
    public static string Build(Guid token, string? fromAddress) =>
        $"{token:N}@{DomainOf(fromAddress)}";

    private static string DomainOf(string? address)
    {
        var at = address?.LastIndexOf('@') ?? -1;
        return at >= 0 && at < address!.Length - 1
            ? address[(at + 1)..]
            : FallbackDomain;
    }
}
