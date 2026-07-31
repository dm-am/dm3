using System;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Web.API.Shared.Authentication.Credentials;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Middleware;

/// <summary>
/// Works out which account a request belongs to, for the rate limiter alone.
/// </summary>
/// <remarks>
/// The limiter runs before authentication, and has to: it is what stops a flood
/// before the flood reaches the database. That leaves its partitioner with no
/// identity to count by, and the framework calls that partitioner synchronously,
/// so it cannot go and build one either. The session cookie already names the
/// account and opening it costs an AES-GCM open of eighty bytes and no query, so
/// the answer is worked out here, one middleware ahead of the limiter, and left
/// on the request. The token is opened twice per authenticated request, here and
/// during authentication; that is microseconds against the alternative, which is
/// running the limiter after the work it exists to prevent.
///
/// This is not authentication and must not be read as such. An opened token
/// proves only that this server minted it — whether the session still exists,
/// whether the account does, whether it is banned, is decided later by
/// <see cref="AuthenticationMiddleware" />. For counting requests that is the
/// right answer anyway: a request carrying a logged-out cookie is still that
/// account's request.
/// </remarks>
public class RateLimitAccountMiddleware
{
    private const string AccountItem = "DM.RateLimiting.Account";

    private readonly RequestDelegate next;

    /// <inheritdoc />
    public RateLimitAccountMiddleware(RequestDelegate next)
    {
        this.next = next;
    }

    /// <summary>
    /// Before request
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="credentialsStorage">Reader of the session cookie</param>
    /// <param name="cryptoService">Cipher the session token was minted with</param>
    public async Task InvokeAsync(HttpContext httpContext,
        ICredentialsStorage credentialsStorage,
        ISymmetricCryptoService cryptoService)
    {
        var credentials = await credentialsStorage.ExtractToken(httpContext);
        if (credentials != null)
        {
            var token = await SessionToken.Read(cryptoService, credentials.Token);
            if (token != null)
            {
                httpContext.Items[AccountItem] = token.UserId;
            }
        }

        await next(httpContext);
    }

    /// <summary>
    /// Account this request carries, or null for a guest and for a token this
    /// server did not mint.
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <returns>Account identifier, or null when the request names none</returns>
    public static Guid? Account(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(AccountItem, out var account) ? account as Guid? : null;
}
