using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Configuration;
using DM.Services.Authentication.Dto;
using DM.Web.Core.Authentication.Credentials;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Authentication;

/// <summary>
/// BFF Pattern credentials storage using HttpOnly cookies.
/// Tokens are never exposed to JavaScript - stored securely in HttpOnly cookies.
/// </summary>
/// <remarks>
/// Security features:
/// - HttpOnly: Cookie cannot be accessed by JavaScript (XSS protection)
/// - Secure: Cookie only sent over HTTPS (in production)
/// - SameSite=Strict: Cookie not sent with cross-site requests (CSRF protection)
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

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax, // Lax allows cookies on cross-site navigation (required for mirror transfer)
            Path = "/",
            IsEssential = true
        };

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
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !httpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Lax, // Lax allows cookies on cross-site navigation (required for mirror transfer)
            Path = "/",
            Expires = DateTimeOffset.UnixEpoch,
            IsEssential = true
        };

        httpContext.Response.Cookies.Delete(AuthCookieName, cookieOptions);
        return Task.CompletedTask;
    }
}