using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Domain.Core.Dto;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// The shared database fixture and a configured HttpClient over one WebApplicationFactory.
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
    /// An answer minus its per-request correlation token, fields in order and identifiers as a placeholder,
    /// so that two refusals compare equal exactly when a caller could not tell them apart.
    /// </summary>
    protected static string Masked(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return body;
        }

        using var document = JsonDocument.Parse(body);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return body;
        }

        var fields = string.Join("&", document.RootElement
            .EnumerateObject()
            .Where(p => p.Name != "traceId")
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.GetRawText()}"));
        return Regex.Replace(fields,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "{id}");
    }

    /// <summary>
    /// A request carrying the test authentication headers of the user.
    /// </summary>
    protected static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url, GeneralUser user)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(TestAuthenticationMiddleware.TestUserIdHeader, user.UserId.ToString());
        request.Headers.Add(TestAuthenticationMiddleware.TestUserUsernameHeader, user.Username);
        request.Headers.Add(TestAuthenticationMiddleware.TestUserRoleHeader, user.Role.ToString());
        return request;
    }

    protected static HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string url)
    {
        return CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateTestUser());
    }

    protected static HttpRequestMessage CreateAdminRequest(HttpMethod method, string url)
    {
        return CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateAdminUser());
    }

    public void Dispose()
    {
        // The factory is shared by every test class.
        Client.Dispose();
    }
}
