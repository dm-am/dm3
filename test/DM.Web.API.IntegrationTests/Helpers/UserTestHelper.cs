using System.Net;
using System.Text.Json.Nodes;
using DM.Domain.Core.Tokens;
using System.Net.Http.Json;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Web.API.Shared.Http;
using Microsoft.EntityFrameworkCore;
using AwesomeAssertions;
using OtpNet;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// Activated users with real password hashes, usable for real login flows.
/// </summary>
public static class UserTestHelper
{
    /// <summary>
    /// Registers by email and password, then activates the pending row with the chosen login.
    /// </summary>
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

        // The row keeps only a hash of the secret, so the helper issues one of its own and stamps its hash.
        var secret = Guid.NewGuid();
        pending.SecretHash = ConfirmationSecret.Hash(secret);
        await db.SaveChangesAsync();

        // 3. Activate with chosen login (token in the header, username in body)
        var activateRequest = new { username = login };
        var activate = new HttpRequestMessage(HttpMethod.Post, "/v1/account/activation")
        {
            Content = JsonContent.Create(activateRequest)
        };
        activate.Headers.Add(TokenHeaders.Account, secret.ToString());
        var activateResponse = await client.SendAsync(activate);
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
    /// The last non-empty dm_session value of a response: middleware may unload before login loads.
    /// </summary>
    public static string? ExtractSessionCookie(HttpResponseMessage response) => Cookie(response, "dm_session");

    /// <summary>The last non-empty value a response sets for the named cookie.</summary>
    public static string? Cookie(HttpResponseMessage response, string name)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        string? value = null;
        foreach (var cookie in cookies)
        {
            if (!cookie.StartsWith(name + "=", StringComparison.Ordinal))
            {
                continue;
            }

            var candidate = cookie[(name.Length + 1)..].Split(';')[0];
            if (!string.IsNullOrEmpty(candidate))
            {
                value = candidate;
            }
        }

        return value;
    }

    /// <summary>
    /// Turns the second factor on for a signed-in session: asks for a secret, then
    /// confirms it with a code computed from that secret.
    /// </summary>
    public static async Task<(string Secret, IReadOnlyList<string> Codes)> EnableSecondFactor(
        HttpClient client, string sessionCookie, string password)
    {
        var setup = CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/setup", sessionCookie);
        setup.Content = JsonContent.Create(new { password });
        var setupResponse = await client.SendAsync(setup);
        setupResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var secret = JsonNode.Parse(await setupResponse.Content.ReadAsStringAsync())!
            ["resource"]!["secret"]!.GetValue<string>();

        var confirm = CreateCookieAuthRequest(
            HttpMethod.Post, "/v1/account/two-factor/confirm", sessionCookie);
        confirm.Content = JsonContent.Create(
            new { code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(DateTime.UtcNow) });
        var confirmResponse = await client.SendAsync(confirm);
        confirmResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var codes = JsonNode.Parse(await confirmResponse.Content.ReadAsStringAsync())!
            ["resource"]!["codes"]!.AsArray()
            .Select(node => node!.GetValue<string>())
            .ToList();
        return (secret, codes);
    }

    /// <summary>
    /// Registers and activates an account with a login and address unique per test: the tests share one database.
    /// </summary>
    public static async Task<(string Login, string Email)> CreateUniqueUser(
        HttpClient client, DatabaseFixture dbFixture, string prefix, string password)
    {
        var login = prefix + Guid.NewGuid().ToString("N")[..8];
        var email = $"{login}@test.example.com";
        await CreateActivatedUser(client, dbFixture, login, password, email);
        return (login, email);
    }
}
