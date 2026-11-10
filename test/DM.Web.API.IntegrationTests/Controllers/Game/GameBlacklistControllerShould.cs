using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class GameBlacklistControllerShould : IntegrationTestBase
{
    public GameBlacklistControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBlacklistWhenNotAuthenticatedReturnsUnauthorized()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/blacklist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
