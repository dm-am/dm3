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
/// The limiter runs before authentication and its partitioner is synchronous, so
/// the account is read from the session cookie here, without a query. This is
/// not authentication: <see cref="AuthenticationMiddleware" /> decides whether the
/// session holds, while a logged-out cookie still counts as that account's request.
/// </remarks>
public class RateLimitAccountMiddleware
{
    private const string AccountItem = "DM.RateLimiting.Account";

    private readonly RequestDelegate _next;

    public RateLimitAccountMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ICredentialsStorage credentialsStorage,
        ISymmetricCryptoService cryptoService)
    {
        var credentials = credentialsStorage.ExtractToken(httpContext);
        if (credentials != null)
        {
            var token = await SessionToken.Read(cryptoService, credentials.Token);
            if (token != null)
            {
                httpContext.Items[AccountItem] = token.UserId;
            }
        }

        await _next(httpContext);
    }

    /// <summary>
    /// Account this request carries, or null for a guest and for a token this
    /// server did not mint.
    /// </summary>
    public static Guid? Account(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(AccountItem, out var account) ? account as Guid? : null;
}
