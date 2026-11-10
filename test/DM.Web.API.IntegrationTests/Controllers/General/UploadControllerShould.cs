using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

public class UploadControllerShould : IntegrationTestBase
{
    public UploadControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUploadsRequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/uploads");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUploadsWithAuthReturnsOk()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/uploads");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUploadRequiresAuthentication()
    {
        var uploadId = Guid.NewGuid();
        var response = await Client.GetAsync($"/v1/uploads/{uploadId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // A type sent in the form instead of the query binds the enum to zero.
    [Fact]
    public async Task DirectUploadWithoutTypeRefusesAndNamesTheField()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/uploads");
        request.Content = OneFilePart();

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no upload type was named, which is a bad request and not a server " +
            "fault. Body was: {0}", body);
        FieldsRefused(body).Should().Contain("type",
            "the refusal has to say which field is missing, or the caller is left " +
            "to guess at a request they can see nothing wrong with");
    }

    // Binding refuses this one, the guard above the other; the answer must not change between them.
    [Fact]
    public async Task DirectUploadWithUnknownTypeRefusesAndNamesTheField()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Post, "/v1/uploads?type=99");
        request.Content = OneFilePart();

        var response = await Client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "no member of the enum carries 99, so the request names a type this " +
            "site does not have. Body was: {0}", body);
        FieldsRefused(body).Should().Contain("type");
    }

    // A multipart body with the one required part; the type is checked before the file.
    private static MultipartFormDataContent OneFilePart()
    {
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "avatar.png" } };
    }

    private static IEnumerable<string> FieldsRefused(string body) =>
        JsonDocument.Parse(body).RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(field => field.Name);
}
