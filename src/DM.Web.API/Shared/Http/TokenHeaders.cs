using System;
using System.Net;
using DM.Domain.Core.Exceptions;

namespace DM.Web.API.Shared.Http;

/// <summary>
/// Headers carrying the token a token-gated endpoint treats as the caller's
/// credential.
/// </summary>
/// <remarks>
/// A path is written verbatim into the reverse-proxy access log, into the request
/// log and into the trace, so a token placed there is disclosed by construction and
/// rotating it means purging logs rather than changing a value. A header lands in
/// none of the three: neither the nginx combined format nor the default ASP.NET
/// Core tracing instrumentation records request headers.
///
/// The token is the credential of the call and not part of the address of the
/// resource, so it travels the way a credential travels — in a header — for the
/// status check and for the call that completes the change alike. Splitting it
/// (header on GET, body on POST) would give one flow two conventions, and the
/// second one would be copied by the next endpoint.
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
    /// token is: the caller learns that the link does not work and nothing about
    /// why. It also replaces the bare bodyless 404 an unmatched route used to
    /// produce with a sentence the reader can act on — the middleware owns the body
    /// of every refusal, and this one is no exception.
    /// </remarks>
    /// <param name="raw">Raw header value; null when the header is absent</param>
    /// <returns>The parsed token</returns>
    public static Guid ParseAccountToken(string? raw) =>
        Guid.TryParse(raw, out var token)
            ? token
            : throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.LinkInvalidOrExpired);
}
