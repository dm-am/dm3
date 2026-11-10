using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class RemovedSeedEndpointsShould : IntegrationTestBase
{
    public RemovedSeedEndpointsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Theory]
    [InlineData("/v1/moderation/seed")]
    [InlineData("/v1/moderation/seed/comprehensive")]
    public async Task NotExposeSeedEndpoints(string url)
    {
        var response = await Client.PostAsync(url, null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotExposeUserListing()
    {
        var response = await Client.GetAsync("/v1/moderation/users");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotExposeSelfRoleAssignment()
    {
        // Authenticated on purpose: the endpoint required a session,
        // so an anonymous rejection alone would not prove the route is gone
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/moderation/users/me/role/Admin");

        var response = await Client.SendAsync(request);

        // 405 rather than 404 because the path is still owned by the
        // moderator-only PATCH v1/moderation/users/{username}/role/{role}.
        // No POST handler exists any more, so routing rejects the request
        // before any authorization or role change can happen.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
