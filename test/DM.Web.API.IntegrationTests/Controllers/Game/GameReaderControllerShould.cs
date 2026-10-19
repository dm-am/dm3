using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class GameReaderControllerShould : IntegrationTestBase
{
    public GameReaderControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get readers of existing game should return OK
    /// </summary>
    [Fact]
    public async Task GetReadersWithValidIdReturnsOk()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/readers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetReadersWithNonExistentIdReturnsNotFound()
    {
        var nonExistentId = Guid.NewGuid();

        var response = await Client.GetAsync($"/v1/games/{nonExistentId}/readers");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

}
