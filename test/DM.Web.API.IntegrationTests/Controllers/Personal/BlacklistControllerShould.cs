using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Personal;

public class BlacklistControllerShould : IntegrationTestBase
{
    public BlacklistControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBlacklistRequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/users/me/blacklist");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBlacklistWithAuthReturnsOk()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/users/me/blacklist");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
