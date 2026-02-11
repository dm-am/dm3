using DM.Services.Authentication.Dto;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Test middleware that authenticates requests based on special test headers.
/// This middleware runs BEFORE the normal AuthenticationMiddleware and sets up the identity
/// so that the real authentication is bypassed.
/// </summary>
/// <remarks>
/// Headers:
/// - X-Test-User-Id: User ID (GUID) to authenticate as
/// - X-Test-User-Login: User login name
/// - X-Test-User-Role: User role (RegularUser, Admin, Moderator, etc.)
/// </remarks>
public class TestAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public const string TestUserIdHeader = "X-Test-User-Id";
    public const string TestUserLoginHeader = "X-Test-User-Login";
    public const string TestUserRoleHeader = "X-Test-User-Role";

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
            var login = httpContext.Request.Headers.TryGetValue(TestUserLoginHeader, out var loginHeader)
                ? loginHeader.FirstOrDefault() ?? "testuser"
                : "testuser";

            var role = UserRole.RegularUser;
            if (httpContext.Request.Headers.TryGetValue(TestUserRoleHeader, out var roleHeader) &&
                Enum.TryParse<UserRole>(roleHeader.FirstOrDefault(), out var parsedRole))
            {
                role = parsedRole;
            }

            // Create authenticated user
            var authenticatedUser = new AuthenticatedUser
            {
                UserId = userId,
                Login = login,
                Role = role,
                AccessPolicy = AccessPolicy.NotSpecified,
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
                ExpirationDate = DateTimeOffset.UtcNow.AddHours(2)
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
