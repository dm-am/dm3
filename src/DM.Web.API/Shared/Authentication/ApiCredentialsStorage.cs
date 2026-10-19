using System;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Core.Identity;
using DM.Web.API.Shared.Authentication.Credentials;
using DM.Web.API.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// BFF Pattern credentials storage using HttpOnly cookies.
/// Tokens are never exposed to JavaScript - stored securely in HttpOnly cookies.
/// </summary>
/// <remarks>
/// SameSite=Lax withholds the cookie from cross-site subrequests and still sends
/// it on top-level navigation, which the links in letters depend on; CSRF is the
/// origin check middleware's.
/// </remarks>
internal class ApiCredentialsStorage : ICredentialsStorage
{
    /// <summary>
    /// Authentication cookie name
    /// </summary>
    public const string AuthCookieName = "dm_session";

    private readonly AuthenticationConfiguration _config;
    private readonly SessionCookieConfiguration _cookie;

    public ApiCredentialsStorage(
        IOptions<AuthenticationConfiguration> authConfig,
        IOptions<SessionCookieConfiguration> cookieConfig)
    {
        _config = authConfig.Value;
        _cookie = cookieConfig.Value;
    }

    /// <inheritdoc />
    public TokenCredentials? ExtractToken(HttpContext httpContext) =>
        httpContext.Request.Cookies[AuthCookieName] is { Length: > 0 } authToken
            ? new TokenCredentials { Token = authToken }
            : null;

    /// <inheritdoc />
    public Task Load(HttpContext httpContext, IIdentity identity)
    {
        var isPersistent = identity.Session?.Persistent ?? false;

        var cookieOptions = SessionCookieOptions(httpContext, _cookie);

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
        var cookieOptions = SessionCookieOptions(httpContext, _cookie);
        cookieOptions.Expires = DateTimeOffset.UnixEpoch;

        httpContext.Response.Cookies.Delete(AuthCookieName, cookieOptions);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attributes every write of the session cookie, and of the second-factor
    /// challenge cookie, repeats
    /// </summary>
    /// <remarks>
    /// One place on purpose: a browser overwrites a cookie only when the incoming
    /// attributes match the stored ones, so the writing and the clearing halves
    /// diverging leaves a cookie that logging out cannot clear.
    /// </remarks>
    internal static CookieOptions SessionCookieOptions(
        HttpContext httpContext,
        SessionCookieConfiguration config) => new()
        {
            HttpOnly = true,
            // Empty means host-only; SessionCookieConfiguration.Domain says when to name one.
            Domain = string.IsNullOrWhiteSpace(config.Domain)
            ? null
            : config.Domain,
            // Same-as-request: a Secure cookie over plain http is dropped by the
            // browser in silence. IsHttps follows X-Forwarded-Proto from a trusted proxy.
            Secure = httpContext.Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true
        };
}
