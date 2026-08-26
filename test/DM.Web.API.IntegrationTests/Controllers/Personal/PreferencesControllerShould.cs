using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

/// <summary>
/// Integration tests for PreferencesController
/// </summary>
public class PreferencesControllerShould : IntegrationTestBase
{
    public PreferencesControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPreferences_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/users/me/preferences");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPreferences_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/preferences");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePreferences_RequiresAuthentication()
    {
        // Arrange
        var preferencesData = new
        {
            paginationSize = 25,
            colorSchema = "dark"
        };

        // Act
        var response = await Client.PatchAsJsonAsync("/v1/users/me/preferences", preferencesData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
