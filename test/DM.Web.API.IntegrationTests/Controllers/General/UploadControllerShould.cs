using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Integration tests for UploadController.
/// </summary>
public class UploadControllerShould : IntegrationTestBase
{
    public UploadControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUploads_RequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/uploads");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUploads_WithAuth_ReturnsOk()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/uploads");
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUpload_RequiresAuthentication()
    {
        var uploadId = Guid.NewGuid();
        var response = await Client.GetAsync($"/v1/uploads/{uploadId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUpload_RequiresAuthentication()
    {
        var uploadId = Guid.NewGuid();
        var response = await Client.DeleteAsync($"/v1/uploads/{uploadId}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DirectUpload_RequiresAuthentication()
    {
        var response = await Client.PostAsync("/v1/uploads?type=UserAvatar", null);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// An upload that names no type is the caller's mistake and is answered as
    /// one, naming the field they left out.
    /// </summary>
    /// <remarks>
    /// The type is read from the query string, and a multipart request carrying
    /// it in the form instead — the easy mistake on this endpoint — leaves the
    /// enum bound to zero. Zero reached the service, where the switch over the
    /// type had no arm for it and threw, so the caller was handed a 500 and a
    /// support token for a request of their own that was merely incomplete.
    /// </remarks>
    [Fact]
    public async Task DirectUpload_WithoutType_RefusesAndNamesTheField()
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

    /// <summary>
    /// A type outside the enum is refused as a bad request too - by model
    /// binding, which is where a value it cannot read is refused.
    /// </summary>
    /// <remarks>
    /// Held here rather than assumed: the two halves of "the type is no good"
    /// are answered in two different places, and only one of them was ever
    /// wrong. A refactor that makes the parameter nullable to reach the guard
    /// above would move this case out of binding and into the guard, and this
    /// is what says the answer must not change when it does.
    /// </remarks>
    [Fact]
    public async Task DirectUpload_WithUnknownType_RefusesAndNamesTheField()
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

    /// <summary>
    /// A multipart body with the one part the endpoint requires. The bytes are
    /// never read: the type is checked before the file is, and these two tests
    /// are about the check that comes first.
    /// </summary>
    private static MultipartFormDataContent OneFilePart()
    {
        var file = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "avatar.png" } };
    }

    /// <summary>The field names a ValidationProblemDetails body refuses.</summary>
    private static IEnumerable<string> FieldsRefused(string body) =>
        JsonDocument.Parse(body).RootElement
            .GetProperty("errors")
            .EnumerateObject()
            .Select(field => field.Name);
}
