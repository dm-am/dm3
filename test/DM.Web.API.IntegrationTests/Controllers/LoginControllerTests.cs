using System.Net;
using System.Net.Http.Json;
using DM.Web.API.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for LoginController
/// </summary>
public class LoginControllerTests : IntegrationTestBase
{
    public LoginControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region Validation Tests

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsBadRequest()
    {
        var credentials = new { email = "nonexistent@test.example.com", password = "wrongpassword" };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithEmptyEmail_ReturnsBadRequest()
    {
        var credentials = new { email = "", password = "password123" };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsBadRequest()
    {
        var credentials = new { email = "testuser@test.example.com", password = "" };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Logout_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/v1/account/login");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutOthers_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var response = await Client.DeleteAsync("/v1/account/sessions/others");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Happy Path: Successful Login (6.1)

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkWithSession()
    {
        // Arrange — create a real user with a known password
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"loginok{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);

        // Act — login
        var credentials = new { email, password };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(login);

        var cookie = UserTestHelper.ExtractSessionCookie(response);
        cookie.Should().NotBeNullOrEmpty("session cookie must be set on successful login");
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsBadRequest()
    {
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"loginfail{uid}";
        var email = $"{login}@test.example.com";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, "TestPass123ok", email);

        var credentials = new { email, password = "WrongPassword123" };
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Session Lifecycle (6.4)

    [Fact]
    public async Task Login_ThenGetCurrent_ReturnsUser()
    {
        // Arrange
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"sess{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Act — use session cookie to call authenticated endpoint
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", sessionCookie);
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(login);
    }

    [Fact]
    public async Task Logout_InvalidatesSession()
    {
        // Arrange
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"logout{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Act — logout
        var logoutRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Delete, "/v1/account/login", sessionCookie);
        var logoutResponse = await Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — session is no longer valid
        var checkRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", sessionCookie);
        var checkResponse = await Client.SendAsync(checkRequest);
        checkResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LogoutOthers_InvalidatesOtherSessions()
    {
        // Arrange — create user and login twice
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"logoutall{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);
        var session1 = await UserTestHelper.Login(Client, email, password);
        var session2 = await UserTestHelper.Login(Client, email, password);

        // Act — logout elsewhere from session 1 (invalidates all except current)
        var logoutRequest = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Delete, "/v1/account/sessions/others", session1);
        var logoutResponse = await Client.SendAsync(logoutRequest);
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — session 1 (current) is still valid
        var check1 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", session1);
        (await Client.SendAsync(check1)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — session 2 (other) is invalid
        var check2 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Get, "/v1/users/me/profile", session2);
        (await Client.SendAsync(check2)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Account Lockout (6.5)

    [Fact]
    public async Task Login_AfterMaxAttempts_ReturnsLocked()
    {
        // Arrange — create user
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"lockout{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);

        // Act — exhaust attempts (threshold is 5 in test config)
        // The 5th attempt triggers lockout, but itself returns WrongPassword.
        // The 6th attempt hits the lockout check and returns Forbidden.
        var wrongCredentials = new { email, password = "WrongPassword123" };
        for (var i = 0; i < 5; i++)
        {
            await Client.PostAsJsonAsync("/v1/account/login", wrongCredentials);
        }

        var lockedResponse = await Client.PostAsJsonAsync("/v1/account/login", wrongCredentials);

        // Assert — account should be locked (403 Forbidden)
        lockedResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion
}
