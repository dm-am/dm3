using System.Net;
using FluentAssertions;
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
}
