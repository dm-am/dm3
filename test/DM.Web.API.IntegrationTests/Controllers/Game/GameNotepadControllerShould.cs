using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class GameNotepadControllerShould : IntegrationTestBase
{
    public GameNotepadControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Get game notes without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetGameNotesWhenNotAuthenticatedReturnsUnauthorized()
    {
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/notepad");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
