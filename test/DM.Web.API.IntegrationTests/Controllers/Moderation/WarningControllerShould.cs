using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for WarningController
/// </summary>
public class WarningControllerShould : IntegrationTestBase
{
    public WarningControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetWarnings_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/warnings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWarnings_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/warnings");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWarning_RequiresAuthentication()
    {
        // Arrange
        var warningData = new
        {
            userLogin = TestConstants.SecondUserLogin,
            reason = "Misconduct",
            description = "Test warning"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/warnings", warningData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteWarning_RequiresAuthentication()
    {
        // Arrange
        var warningId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/warnings/{warningId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
