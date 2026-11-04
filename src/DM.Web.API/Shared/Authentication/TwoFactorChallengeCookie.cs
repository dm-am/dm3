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
/// A cookie and not a field of the answer: a single-page client would lose it
/// on a refresh halfway through a login, and scripts cannot read it. The
/// attributes are the session cookie's, the value is the challenge identifier in
/// the AEAD envelope of the session token, so a forged one is refused before the
/// database is asked anything.
/// </remarks>
public class TwoFactorChallengeCookie
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
        var options = ApiCredentialsStorage.SessionCookieOptions(httpContext, _cookie);
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
        var options = ApiCredentialsStorage.SessionCookieOptions(httpContext, _cookie);
        options.Expires = DateTimeOffset.UnixEpoch;
        httpContext.Response.Cookies.Delete(CookieName, options);
    }
}
