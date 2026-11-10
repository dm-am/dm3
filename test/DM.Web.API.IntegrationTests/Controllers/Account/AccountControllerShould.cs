using System.Net;
using System.Net.Http.Json;
using DM.Web.API.IntegrationTests.Helpers;
using DM.Web.API.Shared.Http;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

public class AccountControllerShould : IntegrationTestBase
{
    public AccountControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RegisterWithValidDataReturnsCreated()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        // Use a unique password that won't be in HIBP breach database
        var uniquePassword = $"UniqueP@ss{uniqueId}!";
        var registration = new
        {
            email = $"newuser{uniqueId}@example.com",
            password = uniquePassword,
            acceptedRules = true
        };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        // Email-first flow: no Location header since we don't have a user yet
    }

    /// <remarks>A null address is a fresh one; test@example.com belongs to the seed.</remarks>
    [Theory]
    [InlineData(null, "ValidPassword123!", false)]
    [InlineData("", "ValidPassword123!", true)]
    [InlineData("notanemail", "ValidPassword123!", true)]
    [InlineData("valid@example.com", "", true)]
    [InlineData("valid@example.com", "short", true)]
    [InlineData("test@example.com", "ValidPassword123!", true)]
    public async Task RefuseARegistrationWithABadField(string? email, string password, bool acceptedRules)
    {
        var registration = new
        {
            email = email ?? $"newuser{Guid.NewGuid().ToString("N")[..8]}@example.com",
            password,
            acceptedRules
        };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ActivateWithInvalidTokenReturnsGone()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/account/activation")
        {
            Content = JsonContent.Create(new { username = "testlogin" })
        };
        request.Headers.Add(TokenHeaders.Account, Guid.NewGuid().ToString());
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetActivationInfoWithInvalidTokenReturnsNotFound()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/account/activation");
        request.Headers.Add(TokenHeaders.Account, Guid.NewGuid().ToString());
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActivationInfoWithoutTokenHeaderReturnsNotFound()
    {
        // A missing credential is answered exactly as an unknown one: the caller
        // learns that the link does not work and nothing about why.
        var response = await Client.GetAsync("/v1/account/activation");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <remarks>
    /// Posted, not queried. The name being checked is the one value that must
    /// not reach an access log, and the request line is logged with its query.
    /// </remarks>
    [Fact]
    public async Task CheckUsernameWithAvailableUsernameReturnsAvailable()
    {
        var uniqueUsername = $"available{Guid.NewGuid():N}"[..15];
        var response = await Client.PostAsJsonAsync(
            "/v1/account/check-username", new { username = uniqueUsername });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":true");
    }

    [Fact]
    public async Task CheckUsernameWithTakenUsernameReturnsNotAvailable()
    {
        // TestUser is from seed data
        var response = await Client.PostAsJsonAsync(
            "/v1/account/check-username", new { username = TestConstants.TestUserLogin });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":false");
    }

    // The checked identifier never appears in the request line.
    [Fact]
    public async Task CheckUsernameRefusesTheQueryStringForm()
    {
        var response = await Client.GetAsync("/v1/account/check-username?username=whoever");

        // The address is still there, the verb is not: 405 rather than 404.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task GetCurrentWhenAuthenticatedReturnsOkWithUserDetails()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/profile");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.TestUserLogin);
    }

    [Fact]
    public async Task GetCurrentWhenNotAuthenticatedReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/v1/users/me/profile");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrentAsAdminReturnsOkWithAdminDetails()
    {
        var request = CreateAdminRequest(HttpMethod.Get, "/v1/users/me/profile");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.AdminUserLogin);
    }

    [Fact]
    public async Task RequestRecoveryWithEmptyEmailReturnsBadRequest()
    {
        var recoveryData = new { email = "" };
        var response = await Client.PostAsJsonAsync("/v1/account/recovery", recoveryData);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestRecoveryWithNonexistentUserReturnsOk()
    {
        // Recovery answers 200 for an unknown address too, and the body says which
        // of the three outcomes happened. Telling a registered address from an
        // unregistered one is deliberate here and recorded as an exception to the
        // non-disclosure rule, so the form can say an address was mistyped.
        var recoveryData = new { email = "nonexistent@example.com" };
        var response = await Client.PostAsJsonAsync("/v1/account/recovery", recoveryData);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePasswordWithValidOldPasswordReturnsOk()
    {
        // Create user and login
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"pwchg{uid}";
        var email = $"{login}@test.example.com";
        // Use unique passwords that won't be in HIBP breach database
        var oldPassword = $"OldP@ss{uid}!";
        var newPassword = $"NewP@ss{uid}!";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, oldPassword, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, oldPassword);

        // Change password
        var changeData = new { oldPassword, newPassword };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Post, "/v1/account/password", sessionCookie);
        request.Content = JsonContent.Create(changeData);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify: old password no longer works
        var oldLoginResponse = await Client.PostAsJsonAsync("/v1/account/login",
            new { email, password = oldPassword });
        oldLoginResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Verify: new password works
        var newLoginResponse = await Client.PostAsJsonAsync("/v1/account/login",
            new { email, password = newPassword });
        newLoginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangeEmailWithValidPasswordReturnsOk()
    {
        // Create user and login
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"emchg{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        var newEmail = $"new{uid}@test.example.com";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Request email change
        var changeData = new { password, email = newEmail };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Post, "/v1/account/email-change", sessionCookie);
        request.Content = JsonContent.Create(changeData);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

}
