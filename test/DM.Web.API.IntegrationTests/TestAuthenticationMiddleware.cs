using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Authenticates a request from test headers ahead of the normal AuthenticationMiddleware: a test
/// IIdentityProvider would be overwritten by the account module's registration.
/// </summary>
/// <remarks>
/// Headers: X-Test-User-Id, X-Test-User-Username, X-Test-User-Role and X-Test-User-Access-Policy (FullBan,
/// DemocraticBan; without it the identity carries no restriction).
/// </remarks>
public class TestAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public const string TestUserIdHeader = "X-Test-User-Id";
    public const string TestUserUsernameHeader = "X-Test-User-Username";
    public const string TestUserRoleHeader = "X-Test-User-Role";
    public const string TestUserAccessPolicyHeader = "X-Test-User-Access-Policy";

    public TestAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext, IIdentitySetter identitySetter)
    {
        if (httpContext.Request.Headers.TryGetValue(TestUserIdHeader, out var userIdHeader) &&
            Guid.TryParse(userIdHeader.FirstOrDefault(), out var userId))
        {
            var username = httpContext.Request.Headers.TryGetValue(TestUserUsernameHeader, out var usernameHeader)
                ? usernameHeader.FirstOrDefault() ?? "testuser"
                : "testuser";

            var role = UserRole.RegularUser;
            if (httpContext.Request.Headers.TryGetValue(TestUserRoleHeader, out var roleHeader) &&
                Enum.TryParse<UserRole>(roleHeader.FirstOrDefault(), out var parsedRole))
            {
                role = parsedRole;
            }

            var accessPolicy = AccessPolicy.NotSpecified;
            if (httpContext.Request.Headers.TryGetValue(TestUserAccessPolicyHeader, out var policyHeader) &&
                Enum.TryParse<AccessPolicy>(policyHeader.FirstOrDefault(), out var parsedPolicy))
            {
                accessPolicy = parsedPolicy;
            }

            var authenticatedUser = new AuthenticatedUser
            {
                UserId = userId,
                Username = username,
                Role = role,
                AccessPolicy = accessPolicy,
                Salt = "testsalt",
                PasswordHash = "testhash",
                PasswordHashVersion = 3
            };

            var session = new Session
            {
                Id = Guid.NewGuid(),
                Persistent = false,
                ExpirationUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            var identity = Identity.Success(
                authenticatedUser,
                session,
                UserSettings.Default,
                "test-token");

            identitySetter.Current = identity;
        }

        await _next(httpContext);
    }
}
