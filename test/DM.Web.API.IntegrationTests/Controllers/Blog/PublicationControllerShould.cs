using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// Integration tests for PublicationController
/// </summary>
public class PublicationControllerShould : IntegrationTestBase
{
    public PublicationControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetPublications_WithUnknownBlog_ReturnsNotFound()
    {
        // Arrange - a blog id that is guaranteed not to exist
        var blogId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/blogs/{blogId}/publications");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostPublication_RequiresAuthentication()
    {
        // Arrange
        var blogId = Guid.NewGuid();
        var publicationData = new
        {
            title = "Test Publication",
            text = "Test Content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/blogs/{blogId}/publications", publicationData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeletePublication_RequiresAuthentication()
    {
        // Arrange
        var publicationId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/publications/{publicationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
