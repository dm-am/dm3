using System.Net;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The development seeding surface moved out of the API into DM.Tools.Seeder.
/// It was gated by an environment check only, and two of its four endpoints
/// carried no authorization at all — one of them let any authenticated user
/// promote itself to Admin. These tests assert the routes are gone from the
/// application, not merely hidden.
/// </summary>
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
        // Act
        var response = await Client.PostAsync(url, null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotExposeUserListing()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/users");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task NotExposeSelfRoleAssignment()
    {
        // Arrange - authenticated on purpose: the endpoint required a session,
        // so an anonymous rejection alone would not prove the route is gone
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/moderation/users/me/role/Admin");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - 405 rather than 404 because the path is still owned by the
        // moderator-only PATCH v1/moderation/users/{username}/role/{role}.
        // No POST handler exists any more, so routing rejects the request
        // before any authorization or role change can happen.
        response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }
}
