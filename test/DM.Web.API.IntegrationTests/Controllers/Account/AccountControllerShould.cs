using System.Net;
using System.Net.Http.Json;
using DM.Web.API.IntegrationTests.Helpers;
using DM.Web.API.Shared.Http;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// Integration tests for AccountController
/// </summary>
public class AccountControllerShould : IntegrationTestBase
{
    public AccountControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region Register Tests (Email-First Flow)

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
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

    [Fact]
    public async Task Register_WithoutAcceptedRules_ReturnsBadRequest()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registration = new
        {
            email = $"newuser{uniqueId}@example.com",
            password = "ValidPassword123!",
            acceptedRules = false
        };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithEmptyEmail_ReturnsBadRequest()
    {
        var registration = new { email = "", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var registration = new { email = "notanemail", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithEmptyPassword_ReturnsBadRequest()
    {
        var registration = new { email = "valid@example.com", password = "", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var registration = new { email = "valid@example.com", password = "short", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithExistingUserEmail_ReturnsBadRequest()
    {
        // test@example.com is used by seed data
        var registration = new { email = "test@example.com", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account/register", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Activate Tests

    [Fact]
    public async Task Activate_WithInvalidToken_ReturnsGone()
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
    public async Task GetActivationInfo_WithInvalidToken_ReturnsNotFound()
    {
        // The address asserted here used to be /v1/account/activate/{token}, which
        // no route has ever matched: the 404 came from the framework for an
        // unrouted path and the endpoint was never reached at all.
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/account/activation");
        request.Headers.Add(TokenHeaders.Account, Guid.NewGuid().ToString());
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetActivationInfo_WithoutTokenHeader_ReturnsNotFound()
    {
        // A missing credential is answered exactly as an unknown one: the caller
        // learns that the link does not work and nothing about why.
        var response = await Client.GetAsync("/v1/account/activation");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckUsername_WithAvailableUsername_ReturnsAvailable()
    {
        var uniqueUsername = $"available{Guid.NewGuid():N}"[..15];
        var response = await Client.GetAsync($"/v1/account/check-username?username={uniqueUsername}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":true");
    }

    [Fact]
    public async Task CheckUsername_WithTakenUsername_ReturnsNotAvailable()
    {
        // TestUser is from seed data
        var response = await Client.GetAsync($"/v1/account/check-username?username={TestConstants.TestUserUsername}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":false");
    }

    #endregion

    #region GetCurrent Tests

    [Fact]
    public async Task GetCurrent_WhenAuthenticated_ReturnsOkWithUserDetails()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/profile");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.TestUserLogin);
    }

    [Fact]
    public async Task GetCurrent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/v1/users/me/profile");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrent_AsAdmin_ReturnsOkWithAdminDetails()
    {
        var request = CreateAdminRequest(HttpMethod.Get, "/v1/users/me/profile");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.AdminUserLogin);
    }

    #endregion

    #region ResetPassword Tests

    [Fact]
    public async Task RequestRecovery_WithEmptyEmail_ReturnsBadRequest()
    {
        var recoveryData = new { email = "" };
        var response = await Client.PostAsJsonAsync("/v1/account/recovery", recoveryData);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RequestRecovery_WithNonexistentUser_ReturnsOk()
    {
        // Recovery always returns OK to prevent email enumeration
        var recoveryData = new { email = "nonexistent@example.com" };
        var response = await Client.PostAsJsonAsync("/v1/account/recovery", recoveryData);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region ChangePassword Tests (validation)

    [Fact]
    public async Task ChangePassword_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var changeData = new { oldPassword = "oldpassword", newPassword = "newpassword123" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/account/password")
        {
            Content = JsonContent.Create(changeData)
        };
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithEmptyOldPassword_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var changeData = new { oldPassword = "", newPassword = "newpassword123" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/account/password")
        {
            Content = JsonContent.Create(changeData)
        };
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Happy Path: Password Change (6.2)

    [Fact]
    public async Task ChangePassword_WithValidOldPassword_ReturnsOk()
    {
        // Arrange — create user and login
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"pwchg{uid}";
        var email = $"{login}@test.example.com";
        // Use unique passwords that won't be in HIBP breach database
        var oldPassword = $"OldP@ss{uid}!";
        var newPassword = $"NewP@ss{uid}!";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, oldPassword, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, oldPassword);

        // Act — change password
        var changeData = new { oldPassword, newPassword };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Post, "/v1/account/password", sessionCookie);
        request.Content = JsonContent.Create(changeData);
        var response = await Client.SendAsync(request);

        // Assert
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

    #endregion

    #region Happy Path: Email Change (6.3)

    [Fact]
    public async Task ChangeEmail_WithValidPassword_ReturnsOk()
    {
        // Arrange — create user and login
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"emchg{uid}";
        var email = $"{login}@test.example.com";
        var password = "TestPass123ok";
        var newEmail = $"new{uid}@test.example.com";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, password, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, password);

        // Act — request email change
        var changeData = new { password, email = newEmail };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Post, "/v1/account/email-change", sessionCookie);
        request.Content = JsonContent.Create(changeData);
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region ChangeEmail Tests (validation)

    [Fact]
    public async Task ChangeEmail_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var changeData = new { email = "newemail@example.com", password = "password" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/account/email-change")
        {
            Content = JsonContent.Create(changeData)
        };
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangeEmail_WithInvalidEmail_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var changeData = new { email = "notanemail", password = "password123" };
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/account/email-change")
        {
            Content = JsonContent.Create(changeData)
        };
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

}
