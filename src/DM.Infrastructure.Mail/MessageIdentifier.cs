using System;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Message-Id of an outgoing letter.
/// </summary>
/// <remarks>
/// RFC 5322 spells a msg-id as id-left "@" id-right, and the header used to
/// carry a bare GUID, which is neither half of one. A malformed value is the
/// relay's to replace, and the replacement is what a bounce report and the
/// reader's client quote back, so the identifier the sender wrote could not be
/// found again anywhere.
///
/// The right-hand side is the domain of the address this installation sends
/// from, because that is the only domain it can claim an identifier in. It lives
/// beside the SMTP configuration rather than in the worker, so the one piece of
/// protocol knowledge here is testable without a broker and a relay.
/// </remarks>
public static class MessageIdentifier
{
    /// <summary>
    /// Domain used when the configured sender address carries none. Only a
    /// misconfigured host reaches it: the same address is handed to a mailbox a
    /// line later, and an address without a domain does not survive that either —
    /// but a header that ends in an at sign is exactly the malformed value this
    /// exists to avoid, so it must not be the way that failure shows up.
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
