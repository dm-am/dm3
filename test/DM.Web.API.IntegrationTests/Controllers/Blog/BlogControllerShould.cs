using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Blog;

/// <summary>
/// Integration tests for BlogController
/// </summary>
public class BlogControllerShould : IntegrationTestBase
{
    public BlogControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetBlogs_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/blogs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetPopularBlogs_ReturnsOk()
    {
        // Act - Popular blogs use sortBy=popularity query parameter
        var response = await Client.GetAsync("/v1/blogs?sortBy=popularity&take=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #region Search Tests

    /// <summary>
    /// Search blogs should return OK with results
    /// </summary>
    [Fact]
    public async Task GetBlogs_WithSearch_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/blogs?search=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Search blogs with explicit sort should respect sort (not use relevance)
    /// </summary>
    [Fact]
    public async Task GetBlogs_WithSearchAndSort_ReturnsOk()
    {
        // Act - search with explicit created sort (should not use relevance)
        var response = await Client.GetAsync("/v1/blogs?search=test&sortBy=created");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search blogs with updated sort should return OK
    /// </summary>
    [Fact]
    public async Task GetBlogs_WithSearchAndUpdatedSort_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/blogs?search=test&sortBy=updated");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Search blogs with pagination should return OK
    /// </summary>
    [Fact]
    public async Task GetBlogs_WithSearchAndPaging_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/blogs?search=test&size=5&number=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get blogs with popularity sort (routed to GetPopularBlogs) should return OK
    /// </summary>
    [Fact]
    public async Task GetBlogs_WithPopularitySort_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/blogs?sortBy=popularity");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    [Fact]
    public async Task GetUserBlogs_WithValidUser_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/blogs/owner/{TestConstants.TestUserLogin}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostBlog_RequiresAuthentication()
    {
        // Arrange
        var blogData = new { title = "Test Blog", description = "Test Description" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/blogs", blogData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBlog_RequiresAuthentication()
    {
        // Arrange
        var blogId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/blogs/{blogId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PatchBlog_RequiresAuthentication()
    {
        // Arrange
        var blogId = Guid.NewGuid();
        var updateData = new { title = "Updated Title" };

        // Act
        var response = await Client.PatchAsJsonAsync($"/v1/blogs/{blogId}", updateData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
