using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for UserEndorsementController
/// </summary>
public class UserEndorsementControllerShould : IntegrationTestBase
{
    public UserEndorsementControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUserEndorsements_WithValidUser_ReturnsOk()
    {
        // Act - route is /v1/users/{username}/endorsements
        var response = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}/endorsements");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostUserEndorsement_RequiresAuthentication()
    {
        // Arrange
        var endorsementData = new
        {
            text = "Great player!"
        };

        // Act - route is /v1/users/{username}/endorsements
        var response = await Client.PostAsJsonAsync($"/v1/users/{TestConstants.SecondUserLogin}/endorsements", endorsementData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
