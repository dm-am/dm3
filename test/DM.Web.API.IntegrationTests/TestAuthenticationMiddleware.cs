using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Identity;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Test middleware that authenticates requests based on special test headers.
/// This middleware runs BEFORE the normal AuthenticationMiddleware and sets up the identity
/// so that the real authentication is bypassed.
/// Injecting the identity from inside the pipeline is the only option that holds: a test
/// IIdentityProvider registered from the test container callback is overwritten, because the
/// account domain's module registers the production one after that callback has run.
/// </summary>
/// <remarks>
/// Headers:
/// - X-Test-User-Id: User ID (GUID) to authenticate as
/// - X-Test-User-Username: Username
/// - X-Test-User-Role: User role (RegularUser, Admin, Moderator, etc.)
/// - X-Test-User-Access-Policy: Access policy (FullBan, DemocraticBan). Without
///   it the injected identity carries no restriction, which is why a banned
///   subject used to be impossible to express in an integration test.
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
        // Check for test authentication headers
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

            // Create authenticated user
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

            // Create session
            var session = new Session
            {
                Id = Guid.NewGuid(),
                Persistent = false,
                Invisible = false,
                ExpirationUtc = DateTimeOffset.UtcNow.AddHours(2)
            };

            // Create and set identity
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
