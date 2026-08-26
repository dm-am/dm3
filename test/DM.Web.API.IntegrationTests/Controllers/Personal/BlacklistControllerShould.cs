using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Integration tests for BlacklistController
/// </summary>
public class BlacklistControllerShould : IntegrationTestBase
{
    public BlacklistControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBlacklist_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/blacklist");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBlacklist_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/blacklist");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AddToBlacklist_RequiresAuthentication()
    {
        // Arrange
        var blacklistData = new { userLogin = TestConstants.SecondUserLogin };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/users/me/blacklist", blacklistData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveFromBlacklist_RequiresAuthentication()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/users/me/blacklist/{TestConstants.SecondUserLogin}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
