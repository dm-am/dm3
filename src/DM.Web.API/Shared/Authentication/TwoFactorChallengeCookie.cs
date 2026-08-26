using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Security;
using DM.Web.API.Shared.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Shared.Authentication;

/// <summary>
/// The carrier of a login that is waiting for its second factor.
/// </summary>
/// <remarks>
/// A cookie and not a field of the answer. The browser sends it by itself, the
/// CSRF middleware already covers the endpoint that reads it, and its value is
/// invisible to scripts. In the body it would have to live in the memory of a
/// single-page client, where a refresh halfway through a login loses it and the
/// obvious repair is local storage.
///
/// The attributes are the session cookie's, minus the lifetime: five minutes
/// rather than a year. The value is the challenge identifier inside the same AEAD
/// envelope the session token uses, so a forged identifier is refused before the
/// database is asked anything.
/// </remarks>
internal class TwoFactorChallengeCookie
{
    /// <summary>Name of the cookie.</summary>
    public const string CookieName = "dm_2fa";

    private readonly ISymmetricCryptoService _cryptoService;
    private readonly SessionCookieConfiguration _cookie;

    public TwoFactorChallengeCookie(
        ISymmetricCryptoService cryptoService,
        IOptions<SessionCookieConfiguration> cookieConfig)
    {
        _cryptoService = cryptoService;
        _cookie = cookieConfig.Value;
    }

    /// <summary>
    /// Hand the challenge to the browser.
    /// </summary>
    /// <param name="httpContext">Request being answered</param>
    /// <param name="challengeId">Challenge the password step issued</param>
    /// <param name="lifetime">How long the challenge lives</param>
    public async Task Write(HttpContext httpContext, Guid challengeId, TimeSpan lifetime)
    {
        var options = Options(httpContext);
        options.MaxAge = lifetime;
        httpContext.Response.Cookies.Append(
            CookieName, await _cryptoService.Encrypt(challengeId.ToString()), options);
    }

    /// <summary>
    /// The challenge the request carries, or null.
    /// </summary>
    /// <remarks>
    /// A value that does not decrypt is null rather than an exception: a forged
    /// or stale cookie is answered by the same refusal as a wrong code, and the
    /// database is never asked about it.
    /// </remarks>
    /// <param name="httpContext">Request being served</param>
    public async Task<Guid?> Read(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(CookieName, out var value) ||
            string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            return Guid.TryParse(await _cryptoService.Decrypt(value), out var challengeId)
                ? challengeId
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Take the challenge away.
    /// </summary>
    /// <param name="httpContext">Request being answered</param>
    public void Clear(HttpContext httpContext)
    {
        var options = Options(httpContext);
        options.Expires = DateTimeOffset.UnixEpoch;
        httpContext.Response.Cookies.Delete(CookieName, options);
    }

    /// <summary>
    /// The attributes every write of this cookie repeats.
    /// </summary>
    /// <remarks>
    /// One place, for the reason the session cookie has one: a browser replaces a
    /// cookie only when the incoming attributes match the stored ones, so the two
    /// halves drifting apart leaves a challenge nothing can clear.
    /// </remarks>
    private CookieOptions Options(HttpContext httpContext) => new()
    {
        HttpOnly = true,
        Domain = string.IsNullOrWhiteSpace(_cookie.Domain) ? null : _cookie.Domain,
        Secure = httpContext.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true
    };
}
