using System.Net;
using System.Net.Http.Json;
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

    /// <summary>
    /// Login endpoint should return 400 for invalid credentials
    /// </summary>
    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsBadRequest()
    {
        // Arrange
        var credentials = new { login = "nonexistent", password = "wrongpassword" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Login endpoint should return 400 for empty login
    /// </summary>
    [Fact]
    public async Task Login_WithEmptyLogin_ReturnsBadRequest()
    {
        // Arrange
        var credentials = new { login = "", password = "password123" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Login endpoint should return 400 for empty password
    /// </summary>
    [Fact]
    public async Task Login_WithEmptyPassword_ReturnsBadRequest()
    {
        // Arrange
        var credentials = new { login = "testuser", password = "" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/account/login", credentials);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Logout endpoint should return 401 for unauthenticated user
    /// </summary>
    [Fact]
    public async Task Logout_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync("/v1/account/login");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Logout all endpoint should return 401 for unauthenticated user
    /// </summary>
    [Fact]
    public async Task LogoutAll_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync("/v1/account/login/all");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
