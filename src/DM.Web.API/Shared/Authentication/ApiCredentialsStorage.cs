using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// BFF Pattern credentials storage using HttpOnly cookies.
/// Tokens are never exposed to JavaScript - stored securely in HttpOnly cookies.
/// </summary>
/// <remarks>
/// Security features:
/// - HttpOnly: Cookie cannot be accessed by JavaScript (XSS protection)
/// - Secure: set from the transport of the request, so the cookie is marked
///   Secure on https and plain on http. A Secure cookie handed out over http is
///   dropped by the browser without an error: the login answers 200 and the next
///   request arrives anonymous
/// - SameSite=Lax: Cookie withheld from cross-site subrequests, but still sent
///   on top-level navigation, which activation and password-reset links from
///   email depend on. CSRF is covered by the origin check middleware.
/// - Path=/: Cookie available for all API endpoints
/// </remarks>
internal class ApiCredentialsStorage : ICredentialsStorage
{
    /// <summary>
    /// Authentication cookie name
    /// </summary>
    public const string AuthCookieName = "dm_session";

    private readonly AuthenticationConfiguration _config;

    public ApiCredentialsStorage(IOptions<AuthenticationConfiguration> authConfig)
    {
        _config = authConfig.Value;
    }

    /// <inheritdoc />
    public Task<TokenCredentials?> ExtractToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(AuthCookieName, out var authToken))
        {
            return Task.FromResult<TokenCredentials?>(null);
        }

        return string.IsNullOrEmpty(authToken)
            ? Task.FromResult<TokenCredentials?>(null)
            : Task.FromResult<TokenCredentials?>(new TokenCredentials { Token = authToken });
    }

    /// <inheritdoc />
    public Task Load(HttpContext httpContext, IIdentity identity)
    {
        var isPersistent = identity.Session?.Persistent ?? false;

        var cookieOptions = SessionCookieOptions(httpContext, _config);

        // Persistent sessions ("Remember Me"): cookie survives browser restart
        // Non-persistent sessions: session cookie, deleted when browser closes
        if (isPersistent)
        {
            cookieOptions.MaxAge = TimeSpan.FromDays(_config.PersistentSessionExpirationDays);
        }

        httpContext.Response.Cookies.Append(AuthCookieName, identity.AuthenticationToken ?? string.Empty, cookieOptions);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task Unload(HttpContext httpContext)
    {
        var cookieOptions = SessionCookieOptions(httpContext, _config);
        cookieOptions.Expires = DateTimeOffset.UnixEpoch;

        httpContext.Response.Cookies.Delete(AuthCookieName, cookieOptions);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attributes every write of the session cookie repeats
    /// </summary>
    /// <remarks>
    /// One place on purpose: a browser overwrites a cookie only when the incoming
    /// attributes match the stored ones, so the login and the logout halves
    /// diverging leaves a session that logging out cannot clear.
    /// </remarks>
    private static CookieOptions SessionCookieOptions(
        HttpContext httpContext,
        AuthenticationConfiguration config) => new()
    {
        HttpOnly = true,
        // Empty means host-only, which is what an omitted Domain gives and what
        // every deployment has today. A named domain widens the cookie to every
        // host under it, so one session covers all the addresses the site
        // answers on. It travels through the same helper as the logout half on
        // purpose: a browser replaces a cookie only when the incoming
        // attributes match the stored ones, and Domain is one of them, so the
        // two halves disagreeing leaves a session that logging out cannot
        // clear.
        Domain = string.IsNullOrWhiteSpace(config.SessionCookieDomain)
            ? null
            : config.SessionCookieDomain,
        // Same-as-request, which is the framework's own cookie policy. The host
        // name says nothing about the transport: the deployed stand answers plain
        // http on a domain, and a Secure cookie there is dropped by the browser
        // without a word, so the login returns 200 and the next request arrives
        // anonymous. IsHttps reads X-Forwarded-Proto once a trusted proxy is
        // configured, so a stand that gains TLS starts marking the cookie Secure
        // with no code change.
        Secure = httpContext.Request.IsHttps,
        SameSite = SameSiteMode.Lax, // Sent on top-level navigation so email links keep the session
        Path = "/",
        IsEssential = true
    };
}
