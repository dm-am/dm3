using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers;

/// <summary>
/// Integration tests for UserController
/// </summary>
public class UserControllerShould : IntegrationTestBase
{
    public UserControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
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
        var response = await Client.GetAsync("/v1/users/nonexistentuser123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get user profile by login should return 410 for non-existent user
    /// </summary>
    [Fact]
    public async Task GetUserProfile_WithNonExistentUser_ReturnsGone()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/nonexistentuser123/profile");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get users by role should return OK
    /// </summary>
    [Fact]
    public async Task GetUsersByRole_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/by-role/Admin");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    #region Search Tests

    /// <summary>
    /// Search users should return OK with results
    /// </summary>
    [Fact]
    public async Task GetUsers_WithSearch_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/users?q=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Search users with sorting should return OK
    /// </summary>
    [Fact]
    public async Task GetUsers_WithSearchAndSort_ReturnsOk()
    {
        // Act - search with explicit rating sort
        var response = await Client.GetAsync("/v1/users?q=test&sort=Rating");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search users with lastActivity sort should return OK
    /// </summary>
    [Fact]
    public async Task GetUsers_WithSearchAndLastActivitySort_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/users?q=test&sort=LastActivity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search users with registered sort should return OK
    /// </summary>
    [Fact]
    public async Task GetUsers_WithSearchAndRegisteredSort_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/users?q=test&sort=Registered");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get users with all sort options should return OK
    /// </summary>
    [Theory]
    [InlineData("Name")]
    [InlineData("Rating")]
    [InlineData("LastActivity")]
    [InlineData("Registered")]
    public async Task GetUsers_WithSortOption_ReturnsOk(string sortBy)
    {
        // Act
        var response = await Client.GetAsync($"/v1/users?sort={sortBy}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search users with activity filter should return OK
    /// </summary>
    [Theory]
    [InlineData("Active")]
    [InlineData("All")]
    public async Task GetUsers_WithSearchAndFilter_ReturnsOk(string filter)
    {
        // Act
        var response = await Client.GetAsync($"/v1/users?q=test&filter={filter}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion
}
