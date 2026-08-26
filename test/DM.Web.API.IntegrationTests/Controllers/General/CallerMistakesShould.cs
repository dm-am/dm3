using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A caller mistake must not read as a server fault.
///
/// The error middleware maps <c>HttpException</c> and its kin; everything else
/// falls through to 500 with a LogCritical. Domain services were throwing plain
/// <c>ArgumentException</c> / <c>InvalidOperationException</c> for ordinary bad
/// input in five places — an invalid bot type, a note about oneself, a note
/// about a stranger, a direct chat with oneself, a second ban on an already
/// banned user — so every one of them answered 500 while its own
/// <c>[ProducesResponseType]</c> promised 400 or 404.
///
/// Three of the five are cheap to drive over HTTP and are pinned here by URL;
/// the direct chat with oneself and the second ban are pinned by unit tests over
/// the domain services that raise them. The guard is those endpoints, not the
/// pattern: a plain exception on an input path no test names still answers 500
/// unnoticed.
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
