using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A caller mistake must not read as a server fault: the error middleware maps <c>HttpException</c> and its
/// kin, everything else answers 500. Three endpoints are pinned here by URL, two more by domain unit tests.
/// </summary>
public class CallerMistakesShould : IntegrationTestBase
{
    public CallerMistakesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RejectAnUnknownBotChannel()
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/v1/users/me/notifications/bots/carrier-pigeon");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RefuseANoteAboutOneself()
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/v1/users/me/notes/{TestConstants.TestUserLogin}");
        request.Content = JsonContent.Create(new { text = "заметка о себе" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ReportAMissingSubjectAsNotFound()
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Put,
            "/v1/users/me/notes/nobody-by-that-name");
        request.Content = JsonContent.Create(new { text = "заметка о призраке" });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
