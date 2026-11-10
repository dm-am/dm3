using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

public class GameUserControllerShould : IntegrationTestBase
{
    public GameUserControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // Applicant names nobody on the roster and is no role at all.
    [Fact]
    public async Task RefuseARoleTheRosterDoesNotHold()
    {
        var known = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/users?role=player");
        var unknown = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/users?role=Applicant");

        known.StatusCode.Should().Be(HttpStatusCode.OK, "the same address with a role it holds is answered");
        unknown.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
