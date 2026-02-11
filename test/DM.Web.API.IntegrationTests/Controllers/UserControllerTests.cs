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
        var response = await Client.GetAsync("/v1/users/nonexistentuser123");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
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
}
