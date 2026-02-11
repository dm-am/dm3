using System.Net;
using System.Net.Http.Json;
using DM.Web.API.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Account;

/// <summary>
/// Integration tests for AccountController
/// </summary>
public class AccountControllerTests : IntegrationTestBase
{
    public AccountControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region Register Tests (Email-First Flow)

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registration = new
        {
            email = $"newuser{uniqueId}@example.com",
            password = "ValidPassword123!",
            acceptedRules = true
        };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
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
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithEmptyEmail_ReturnsBadRequest()
    {
        var registration = new { email = "", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var registration = new { email = "notanemail", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithEmptyPassword_ReturnsBadRequest()
    {
        var registration = new { email = "valid@example.com", password = "", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var registration = new { email = "valid@example.com", password = "short", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithExistingUserEmail_ReturnsBadRequest()
    {
        // test@example.com is used by seed data
        var registration = new { email = "test@example.com", password = "ValidPassword123!", acceptedRules = true };
        var response = await Client.PostAsJsonAsync("/v1/account", registration);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Activate Tests

    [Fact]
    public async Task Activate_WithInvalidToken_ReturnsGone()
    {
        var invalidToken = Guid.NewGuid();
        var activateRequest = new { token = invalidToken, login = "testlogin" };
        var response = await Client.PostAsJsonAsync("/v1/account/activate", activateRequest);
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task GetActivationInfo_WithInvalidToken_ReturnsNotFound()
    {
        var invalidToken = Guid.NewGuid();
        var response = await Client.GetAsync($"/v1/account/activate/{invalidToken}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CheckLogin_WithAvailableLogin_ReturnsAvailable()
    {
        var uniqueLogin = $"available{Guid.NewGuid():N}"[..15];
        var response = await Client.GetAsync($"/v1/account/check-login?login={uniqueLogin}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":true");
    }

    [Fact]
    public async Task CheckLogin_WithTakenLogin_ReturnsNotAvailable()
    {
        // TestUser is from seed data
        var response = await Client.GetAsync($"/v1/account/check-login?login={TestConstants.TestUserLogin}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("\"isAvailable\":false");
    }

    #endregion

    #region GetCurrent Tests

    [Fact]
    public async Task GetCurrent_WhenAuthenticated_ReturnsOkWithUserDetails()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/account");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.TestUserLogin);
    }

    [Fact]
    public async Task GetCurrent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/v1/account");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCurrent_AsAdmin_ReturnsOkWithAdminDetails()
    {
        var request = CreateAdminRequest(HttpMethod.Get, "/v1/account");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.AdminUserLogin);
    }

    #endregion

    #region ResetPassword Tests

    [Fact]
    public async Task ResetPassword_WithEmptyLogin_ReturnsBadRequest()
    {
        var resetData = new { login = "", email = "test@example.com" };
        var response = await Client.PostAsJsonAsync("/v1/account/password", resetData);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithNonexistentUser_ReturnsOk()
    {
        var resetData = new { login = "nonexistentuser", email = "nonexistent@example.com" };
        var response = await Client.PostAsJsonAsync("/v1/account/password", resetData);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region ChangePassword Tests (validation)

    [Fact]
    public async Task ChangePassword_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        var changeData = new { oldPassword = "oldpassword", newPassword = "newpassword123" };
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/account/password")
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
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/account/password")
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
        var oldPassword = "OldPassword123";
        var newPassword = "NewPassword456";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, oldPassword, email);
        var sessionCookie = await UserTestHelper.Login(Client, email, oldPassword);

        // Act — change password
        var changeData = new { oldPassword, newPassword };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Patch, "/v1/account/password", sessionCookie);
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

        // Act — change email
        var changeData = new { password, email = newEmail };
        var request = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Patch, "/v1/account/email", sessionCookie);
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
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/account/email")
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
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/account/email")
        {
            Content = JsonContent.Create(changeData)
        };
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Password Reuse Rejection (6.6)

    [Fact]
    public async Task ChangePassword_ToRecentlyUsedPassword_ReturnsBadRequest()
    {
        // Arrange — create user
        var uid = Guid.NewGuid().ToString("N")[..8];
        var login = $"pwreuse{uid}";
        var email = $"{login}@test.example.com";
        var passwordA = "PasswordAAA111";
        var passwordB = "PasswordBBB222";
        var passwordC = "PasswordCCC333";
        await UserTestHelper.CreateActivatedUser(Client, DatabaseFixture, login, passwordA, email);

        // Change A → B
        var session1 = await UserTestHelper.Login(Client, email, passwordA);
        var req1 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Patch, "/v1/account/password", session1);
        req1.Content = JsonContent.Create(new { oldPassword = passwordA, newPassword = passwordB });
        var res1 = await Client.SendAsync(req1);
        res1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Change B → C
        var session2 = await UserTestHelper.Login(Client, email, passwordB);
        var req2 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Patch, "/v1/account/password", session2);
        req2.Content = JsonContent.Create(new { oldPassword = passwordB, newPassword = passwordC });
        var res2 = await Client.SendAsync(req2);
        res2.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act — try to change C → A (reuse)
        var session3 = await UserTestHelper.Login(Client, email, passwordC);
        var req3 = UserTestHelper.CreateCookieAuthRequest(HttpMethod.Patch, "/v1/account/password", session3);
        req3.Content = JsonContent.Create(new { oldPassword = passwordC, newPassword = passwordA });
        var res3 = await Client.SendAsync(req3);

        // Assert — should be rejected due to password reuse
        res3.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
