using System.Net;
using System.Net.Http.Json;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

public class LoginControllerShould : IntegrationTestBase
{
    public LoginControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <remarks>The system author has an empty salt and hash; its refusal is by role, not by those columns.</remarks>
    [Theory]
    [InlineData("nonexistent@test.example.com", "wrongpassword")]
    [InlineData("", "password123")]
    [InlineData("testuser@test.example.com", "")]
    [InlineData("system@dm.local", "anything123")]
    public async Task RefuseALoginThatNamesNoAccountItCanOpen(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/v1/account/login", new { email, password });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginWithValidCredentialsReturnsOkWithSession()
    {
        // Create a real user with a known password
        var password = "TestPass123ok";
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "loginok", password);

        // Login
        var credentials = new { email, password };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(login);

        var cookie = UserTestHelper.ExtractSessionCookie(response);
        cookie.Should().NotBeNullOrEmpty("session cookie must be set on successful login");
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturnsBadRequest()
    {
        var (_, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "loginfail", "TestPass123ok");

        var credentials = new { email, password = "WrongPassword123" };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LoginThenGetCurrentReturnsUser()
    {
        var password = "TestPass123ok";
        var (login, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "sess", password);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Use session cookie to call authenticated endpoint
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", sessionCookie);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(login);
    }

    [Fact]
    public async Task LogoutInvalidatesSession()
    {
        var password = "TestPass123ok";
        var (_, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "logout", password);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Logout
        var logoutRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Delete, "/v1/account/login", sessionCookie);
        var logoutResponse = await Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Session is no longer valid
        var checkRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", sessionCookie);
        var checkResponse = await Client.SendAsync(checkRequest);
        checkResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutOthersInvalidatesOtherSessions()
    {
        // Create user and login twice
        var password = "TestPass123ok";
        var (_, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "logoutall", password);
        var session1 = await UserTestHelper.Login(Client, email, password);
        var session2 = await UserTestHelper.Login(Client, email, password);

        // Logout elsewhere from session 1 (invalidates all except current)
        var logoutRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Delete, "/v1/account/sessions/others", session1);
        var logoutResponse = await Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Session 1 (current) is still valid
        var check1 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", session1);
        (await Client.SendAsync(check1)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Session 2 (other) is invalid
        var check2 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", session2);
        (await Client.SendAsync(check2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginAfterMaxAttemptsReturnsLocked()
    {
        // Create user
        var password = "TestPass123ok";
        var (_, email) = await UserTestHelper.CreateUniqueUser(Client, DatabaseFixture, "lockout", password);

        // Exhaust attempts (threshold is 5 in test config)
        // The 5th attempt triggers lockout, but itself returns WrongPassword.
        // The 6th attempt hits the lockout check and returns Forbidden.
        var wrongCredentials = new { email, password = "WrongPassword123" };
        for (var i = 0; i < 5; i++)
        {
            await Client.PostAsJsonAsync("/v1/account/login", wrongCredentials);
        }

        var lockedResponse = await Client.PostAsJsonAsync("/v1/account/login", wrongCredentials);

        // Account should be locked (403 Forbidden)
        lockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

}
