using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for BanController
/// </summary>
public class BanControllerShould : IntegrationTestBase
{
    public BanControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBans_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/bans");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBans_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/bans");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostBan_RequiresAuthentication()
    {
        // Arrange
        var banData = new
        {
            userLogin = TestConstants.SecondUserLogin,
            reason = "Violation",
            duration = "P7D"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/bans", banData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBan_RequiresAuthentication()
    {
        // Arrange
        var banId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/bans/{banId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
