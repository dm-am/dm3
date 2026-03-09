using System.Net;
using System.Net.Http.Json;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Account;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// Helper for creating activated users with real password hashes in integration tests.
/// Users created through this helper can be used for real login flows.
/// </summary>
public static class UserTestHelper
{
    /// <summary>
    /// Register a new user using email-first flow:
    /// 1. Register with email + password (no login)
    /// 2. Find PendingRegistration token in DB
    /// 3. Activate with chosen login
    /// Returns the login of the created user.
    /// </summary>
    /// <param name="client">HttpClient to use for API calls</param>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="login">Desired login (chosen at activation)</param>
    /// <param name="password">Desired password (must meet policy: 10+ chars, upper+lower+digit)</param>
    /// <param name="email">Desired email</param>
    public static async Task<string> CreateActivatedUser(
        HttpClient client,
        DatabaseFixture dbFixture,
        string login,
        string password,
        string? email = null)
    {
        email ??= $"{login}@test.example.com";

        // 1. Register (email-first flow - no login at this stage)
        var registration = new { email, password, acceptedRules = true };
        var registerResponse = await client.PostAsJsonAsync("/v1/account/register", registration);
        if (registerResponse.StatusCode != HttpStatusCode.Created)
        {
            var body = await registerResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Registration failed with {registerResponse.StatusCode}: {body}");
        }

        // 2. Find PendingRegistration token in DB
        await using var db = dbFixture.CreateDbContext();
        var pending = await db.Set<PendingRegistration>()
            .Where(p => p.Email == email.ToLowerInvariant())
            .FirstOrDefaultAsync();

        if (pending == null)
        {
            throw new InvalidOperationException(
                $"PendingRegistration not found for email '{email}'");
        }

        // 3. Activate with chosen login (token in URL, username in body)
        var activateRequest = new { username = login };
        var activateResponse = await client.PostAsJsonAsync($"/v1/account/activation/{pending.TokenId}", activateRequest);
        if (activateResponse.StatusCode != HttpStatusCode.OK)
        {
            var body = await activateResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Activation failed with {activateResponse.StatusCode}: {body}");
        }

        return login;
    }

    /// <summary>
    /// Login with credentials and return the session cookie value.
    /// </summary>
    /// <param name="client">HttpClient to use for API calls</param>
    /// <param name="email">User email</param>
    /// <param name="password">User password</param>
    /// <returns>Session cookie value for subsequent authenticated requests</returns>
    public static async Task<string> Login(
        HttpClient client,
        string email,
        string password)
    {
        var credentials = new { email, password };
        var response = await client.PostAsJsonAsync("/v1/account/login", credentials);

        if (response.StatusCode != HttpStatusCode.OK)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Login failed with {response.StatusCode}: {body}");
        }

        // Extract session cookie from Set-Cookie header
        var sessionCookie = ExtractSessionCookie(response);
        if (sessionCookie == null)
        {
            throw new InvalidOperationException(
                "Login succeeded but no dm_session cookie was set");
        }

        return sessionCookie;
    }

    /// <summary>
    /// Create an HttpRequestMessage with session cookie authentication.
    /// </summary>
    public static HttpRequestMessage CreateCookieAuthRequest(
        HttpMethod method, string url, string sessionCookie)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $"dm_session={sessionCookie}");
        return request;
    }

    /// <summary>
    /// Extract the dm_session cookie value from a response.
    /// Returns the last non-empty value because the response may contain multiple
    /// Set-Cookie headers for dm_session (e.g. middleware Unload followed by Login Load).
    /// </summary>
    public static string? ExtractSessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
            return null;

        string? result = null;
        foreach (var cookie in cookies)
        {
            if (cookie.StartsWith("dm_session=", StringComparison.OrdinalIgnoreCase))
            {
                var value = cookie.Split(';')[0]; // "dm_session=value"
                var token = value["dm_session=".Length..];
                if (!string.IsNullOrEmpty(token))
                {
                    result = token;
                }
            }
        }

        return result;
    }
}
