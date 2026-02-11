using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Base class for integration tests.
/// Provides access to the shared database fixture and a configured HttpClient.
/// Uses a shared WebApplicationFactory to avoid resource exhaustion.
/// </summary>
[Collection("Database")]
public abstract class IntegrationTestBase : IDisposable
{
    protected readonly DatabaseFixture DatabaseFixture;
    protected readonly HttpClient Client;

    protected IntegrationTestBase(DatabaseFixture databaseFixture)
    {
        DatabaseFixture = databaseFixture;
        // Use shared factory from fixture to avoid creating multiple hosts.
        // Disable automatic cookie handling: CookieContainer would accumulate
        // cookies across requests and contaminate subsequent requests with old
        // session tokens. Tests manage cookies explicitly via CreateCookieAuthRequest.
        Client = databaseFixture.Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = false
        });
    }

    /// <summary>
    /// Create an HttpRequestMessage with test authentication headers.
    /// This is the preferred way to test authenticated endpoints.
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="url">Request URL</param>
    /// <param name="user">User to authenticate as</param>
    /// <returns>HttpRequestMessage with authentication headers</returns>
    protected static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, GeneralUser user)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthenticationMiddleware.TestUserIdHeader, user.UserId.ToString());
        request.Headers.Add(TestAuthenticationMiddleware.TestUserLoginHeader, user.Login);
        request.Headers.Add(TestAuthenticationMiddleware.TestUserRoleHeader, user.Role.ToString());
        return request;
    }

    /// <summary>
    /// Create an HttpRequestMessage authenticated as the test user.
    /// </summary>
    protected static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
    {
        return CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateTestUser());
    }

    /// <summary>
    /// Create an HttpRequestMessage authenticated as admin.
    /// </summary>
    protected static HttpRequestMessage CreateAdminRequest(HttpMethod method, string url)
    {
        return CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateAdminUser());
    }

    /// <summary>
    /// Create a new factory with an authenticated user.
    /// Note: This creates a separate factory that must be disposed.
    /// @deprecated Use CreateAuthenticatedRequest instead - it's simpler and doesn't require factory disposal.
    /// </summary>
    [Obsolete("Use CreateAuthenticatedRequest instead - it's simpler and doesn't require factory disposal.")]
    protected CustomWebApplicationFactory CreateAuthenticatedFactory(GeneralUser user)
    {
        return new CustomWebApplicationFactory(DatabaseFixture)
        {
            TestUser = user
        };
    }

    public void Dispose()
    {
        // Only dispose the client, not the shared factory
        Client.Dispose();
    }
}
