using System.Net;
using System.Net.Http.Json;
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

    #region Register Tests

    /// <summary>
    /// Registration with valid data should return Created
    /// </summary>
    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var uniqueId = Guid.NewGuid().ToString("N")[..8];
        var registration = new
        {
            login = $"newuser{uniqueId}",
            email = $"newuser{uniqueId}@example.com",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    /// <summary>
    /// Registration with empty login should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithEmptyLogin_ReturnsBadRequest()
    {
        // Arrange
        var registration = new
        {
            login = "",
            email = "test@example.com",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with empty email should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithEmptyEmail_ReturnsBadRequest()
    {
        // Arrange
        var registration = new
        {
            login = "validlogin",
            email = "",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with invalid email format should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var registration = new
        {
            login = "validlogin",
            email = "notanemail",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with empty password should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var registration = new
        {
            login = "validlogin",
            email = "valid@example.com",
            password = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with short password should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        // Arrange
        var registration = new
        {
            login = "validlogin",
            email = "valid@example.com",
            password = "short"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with duplicate login should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithDuplicateLogin_ReturnsBadRequest()
    {
        // Arrange - using existing test user login
        var registration = new
        {
            login = TestConstants.TestUserLogin,
            email = "unique@example.com",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Registration with duplicate email should return BadRequest
    /// </summary>
    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange - using existing test user email
        var registration = new
        {
            login = "uniquelogin",
            email = "test@example.com",
            password = "ValidPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account", registration);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region Activate Tests

    /// <summary>
    /// Activation with invalid token should return BadRequest, Gone, or NotFound
    /// </summary>
    [Fact]
    public async Task Activate_WithInvalidToken_ReturnsError()
    {
        // Arrange
        var invalidToken = Guid.NewGuid();

        // Act
        var response = await Client.PutAsync($"/v1/account/{invalidToken}", null);

        // Assert - various error codes are acceptable
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Gone,
            HttpStatusCode.NotFound);
    }

    #endregion

    #region GetCurrent Tests

    /// <summary>
    /// GetCurrent when authenticated should return user details
    /// </summary>
    [Fact]
    public async Task GetCurrent_WhenAuthenticated_ReturnsOkWithUserDetails()
    {
        // Arrange
        var factory = CreateAuthenticatedFactory(CustomWebApplicationFactory.CreateTestUser());
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.TestUserLogin);
    }

    /// <summary>
    /// GetCurrent when not authenticated should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetCurrent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// GetCurrent as admin should return admin details
    /// </summary>
    [Fact]
    public async Task GetCurrent_AsAdmin_ReturnsOkWithAdminDetails()
    {
        // Arrange
        var factory = CreateAuthenticatedFactory(CustomWebApplicationFactory.CreateAdminUser());
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/v1/account");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(TestConstants.AdminUserLogin);
    }

    #endregion

    #region ResetPassword Tests

    /// <summary>
    /// ResetPassword with empty login should return BadRequest
    /// </summary>
    [Fact]
    public async Task ResetPassword_WithEmptyLogin_ReturnsBadRequest()
    {
        // Arrange
        var resetData = new
        {
            login = "",
            email = "test@example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account/password", resetData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// ResetPassword with nonexistent user should return BadRequest
    /// </summary>
    [Fact]
    public async Task ResetPassword_WithNonexistentUser_ReturnsBadRequest()
    {
        // Arrange
        var resetData = new
        {
            login = "nonexistentuser",
            email = "nonexistent@example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account/password", resetData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region ChangePassword Tests

    /// <summary>
    /// ChangePassword when not authenticated should return BadRequest (validation before auth)
    /// </summary>
    [Fact]
    public async Task ChangePassword_WhenNotAuthenticated_ReturnsBadRequest()
    {
        // Arrange
        var changeData = new
        {
            oldPassword = "oldpassword",
            newPassword = "newpassword123"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/v1/account/password", changeData);

        // Assert - validation happens before auth check
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// ChangePassword with empty old password should return BadRequest
    /// </summary>
    [Fact]
    public async Task ChangePassword_WithEmptyOldPassword_ReturnsBadRequest()
    {
        // Arrange
        var changeData = new
        {
            oldPassword = "",
            newPassword = "newpassword123"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/v1/account/password", changeData);

        // Assert - validation happens before auth check
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region ChangeEmail Tests

    /// <summary>
    /// ChangeEmail when not authenticated should return BadRequest (validation before auth)
    /// </summary>
    [Fact]
    public async Task ChangeEmail_WhenNotAuthenticated_ReturnsBadRequest()
    {
        // Arrange
        var changeData = new
        {
            login = "testuser",
            email = "newemail@example.com",
            password = "password"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/v1/account/email", changeData);

        // Assert - validation happens before auth check
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// ChangeEmail with invalid email format should return BadRequest
    /// </summary>
    [Fact]
    public async Task ChangeEmail_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var changeData = new
        {
            login = TestConstants.TestUserLogin,
            email = "notanemail",
            password = "password123"
        };

        // Act
        var response = await Client.PutAsJsonAsync("/v1/account/email", changeData);

        // Assert - validation happens before auth check
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion
}
