using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for UserController
/// </summary>
public class UserControllerTests : IntegrationTestBase
{
    public UserControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get users should return OK with list
    /// </summary>
    [Fact]
    public async Task GetUsers_ReturnsOkWithList()
    {
        // Act
        var response = await Client.GetAsync("/v1/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Get user by login should return 410 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserByLogin_WithNonExistentUser_ReturnsGone()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/by-login/nonexistentuser123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get user by invalid GUID should return proper error
    /// </summary>
    [Fact]
    public async Task GetUserById_WithInvalidGuid_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/users/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get current user without auth should return 401
    /// </summary>
    [Fact]
    public async Task GetCurrentUser_WithoutAuth_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get user details by login should return 410 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserDetails_WithNonExistentUser_ReturnsGone()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/nonexistentuser123/details");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get user settings without auth should work (public endpoint)
    /// but return 410 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserSettings_WithNonExistentUser_ReturnsGone()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/nonexistentuser123/settings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Patch user details without auth should return 401
    /// </summary>
    [Fact]
    public async Task PatchUserDetails_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var userDetails = new { info = "test" };

        // Act
        var response = await Client.PatchAsJsonAsync("/v1/users/testuser/details", userDetails);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Patch user settings without auth should return 401
    /// </summary>
    [Fact]
    public async Task PatchUserSettings_WithoutAuth_ReturnsUnauthorized()
    {
        // Arrange
        var settings = new { };

        // Act
        var response = await Client.PatchAsJsonAsync("/v1/users/testuser/settings", settings);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
