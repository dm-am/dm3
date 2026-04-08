using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Integration tests for UploadController
/// </summary>
public class UploadControllerShould : IntegrationTestBase
{
    public UploadControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUploads_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/uploads");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUploads_WithAuth_ReturnsOk()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/uploads");

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetUpload_RequiresAuthentication()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/uploads/{uploadId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteUpload_RequiresAuthentication()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/uploads/{uploadId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RequestPresignedUrl_RequiresAuthentication()
    {
        // Arrange
        var presignRequest = new
        {
            fileName = "test.jpg",
            contentType = "image/jpeg",
            type = "Avatar"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/uploads/presign", presignRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DirectUpload_RequiresAuthentication()
    {
        // Act
        var response = await Client.PostAsync("/v1/uploads/direct?type=Avatar", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ConfirmUpload_RequiresAuthentication()
    {
        // Arrange
        var uploadId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/uploads/{uploadId}/confirm", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
