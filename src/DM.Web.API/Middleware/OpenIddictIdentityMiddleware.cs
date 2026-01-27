using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.DataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware that bridges OpenIddict authentication with the application's IIdentityProvider system.
/// Runs after UseAuthentication() and populates IIdentityProvider.Current from HttpContext.User.
/// </summary>
public class OpenIddictIdentityMiddleware
{
    private readonly RequestDelegate _next;

    /// <inheritdoc />
    public OpenIddictIdentityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, populating IIdentityProvider from OpenIddict claims
    /// </summary>
    public async Task InvokeAsync(
        HttpContext httpContext,
        IIdentitySetter identitySetter,
        DmDbContext dbContext)
    {
        // If identity is already set (e.g., by tests), skip
        if (identitySetter is IIdentityProvider provider && provider.Current != null)
        {
            await _next(httpContext);
            return;
        }

        // Check if user is authenticated via OpenIddict (JWT Bearer token)
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var identity = await CreateIdentityFromClaims(httpContext.User, dbContext);
            identitySetter.Current = identity;
        }
        else
        {
            // Set guest identity for unauthenticated requests
            identitySetter.Current = Identity.Guest();
        }

        await _next(httpContext);
    }

    private static async Task<IIdentity> CreateIdentityFromClaims(ClaimsPrincipal principal, DmDbContext dbContext)
    {
        // Extract user ID from claims (set by TokenController)
        var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? principal.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Identity.Guest();
        }

        // Load user from database
        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && !u.IsRemoved);

        if (user == null)
        {
            return Identity.Fail(AuthenticationError.Removed);
        }

        // Create authenticated user
        var authenticatedUser = new AuthenticatedUser
        {
            UserId = user.UserId,
            Login = user.Login,
            Role = user.Role,
            AccessPolicy = user.AccessPolicy,
            Salt = user.Salt,
            PasswordHash = user.PasswordHash,
            PasswordHashVersion = user.PasswordHashVersion
        };

        // Create a pseudo-session (OpenIddict manages actual tokens)
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Persistent = true
        };

        // Use default settings (user settings are loaded separately when needed)
        var settings = UserSettings.Default;

        // The auth token is from the Authorization header
        var authHeader = principal.Identity?.AuthenticationType ?? "Bearer";

        return Identity.Success(authenticatedUser, session, settings, authHeader);
    }
}
