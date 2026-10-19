using System;
using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Web.API.Shared.Http;

/// <summary>
/// Headers carrying the token a token-gated endpoint treats as the caller's
/// credential.
/// </summary>
/// <remarks>
/// A path lands in the proxy access log, the request log and the trace, a header
/// in none of them. The token is the credential of the call, so the status check
/// and the call that completes the change both carry it in the header.
/// </remarks>
public static class TokenHeaders
{
    /// <summary>
    /// One-time token from a link the site mailed to the address being proved:
    /// activation, password reset, email change confirmation, username change
    /// approval.
    /// </summary>
    public const string Account = "X-Dm-Account-Token";

    /// <summary>
    /// Tracking token handed to a guest when their ticket was filed, and the only
    /// thing between a stranger and that conversation.
    /// </summary>
    public const string Ticket = "X-Dm-Ticket-Token";

    /// <summary>
    /// The account token a request carries, or a refusal.
    /// </summary>
    /// <remarks>
    /// A missing header and an unparseable value are answered exactly as an unknown
    /// token is: the caller learns that the link does not work and nothing about why.
    /// </remarks>
    /// <param name="raw">Raw header value; null when the header is absent</param>
    /// <returns>The parsed token</returns>
    public static Guid ParseAccountToken(string? raw) =>
        Guid.TryParse(raw, out var token)
            ? token
            : throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
}
