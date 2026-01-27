using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Integration tests for Forum CommentController
/// </summary>
public class CommentControllerTests : IntegrationTestBase
{
    public CommentControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetForumComments Tests

    /// <summary>
    /// Get comments from valid topic should return OK
    /// </summary>
    [Fact]
    public async Task GetForumComments_WithValidTopic_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/topics/{TestConstants.TestTopicId}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Get comments from non-existent topic should return Gone
    /// </summary>
    [Fact]
    public async Task GetForumComments_WithNonExistentTopic_ReturnsGone()
    {
        // Arrange
        var nonExistentTopicId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/topics/{nonExistentTopicId}/comments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    #endregion

    #region PostForumComment Tests

    /// <summary>
    /// Create comment without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostForumComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var comment = new
        {
            text = "Test comment content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/topics/{TestConstants.TestTopicId}/comments", comment);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create comment with empty text should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostForumComment_WithEmptyText_ReturnsUnauthorized()
    {
        // Arrange
        var comment = new
        {
            text = ""
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/topics/{TestConstants.TestTopicId}/comments", comment);

        // Assert - auth check happens before validation
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create comment on non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostForumComment_OnNonExistentTopic_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentTopicId = Guid.NewGuid();
        var comment = new
        {
            text = "Test comment"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/topics/{nonExistentTopicId}/comments", comment);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetForumComment Tests

    /// <summary>
    /// Get existing comment should return Gone (comment not seeded in test DB due to FK constraints)
    /// </summary>
    [Fact]
    public async Task GetForumComment_WithValidId_ReturnsGone()
    {
        // Act
        var response = await Client.GetAsync($"/v1/forum/comments/{TestConstants.TestCommentId}");

        // Assert - comment is not seeded, so returns Gone
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get non-existent comment should return Gone
    /// </summary>
    [Fact]
    public async Task GetForumComment_WithNonExistentId_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/forum/comments/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get comment with invalid GUID should return error
    /// </summary>
    [Fact]
    public async Task GetForumComment_WithInvalidGuid_ReturnsError()
    {
        // Act
        var response = await Client.GetAsync("/v1/forum/comments/not-a-guid");

        // Assert - invalid GUID in route returns 404 Not Found or 400
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region PatchForumComment Tests

    /// <summary>
    /// Update comment without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PatchForumComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var commentUpdate = new
        {
            text = "Updated comment text"
        };
        var content = JsonContent.Create(commentUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/forum/comments/{TestConstants.TestCommentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update non-existent comment should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PatchForumComment_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var commentUpdate = new
        {
            text = "Updated comment text"
        };
        var content = JsonContent.Create(commentUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/forum/comments/{nonExistentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteForumComment Tests

    /// <summary>
    /// Delete comment without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteForumComment_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/forum/comments/{TestConstants.TestCommentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Delete non-existent comment should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteForumComment_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/forum/comments/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PostForumCommentLike Tests

    /// <summary>
    /// Like comment without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostForumCommentLike_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync($"/v1/forum/comments/{TestConstants.TestCommentId}/likes", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Like non-existent comment should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostForumCommentLike_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/forum/comments/{nonExistentId}/likes", null);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteForumCommentLike Tests

    /// <summary>
    /// Remove like without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteForumCommentLike_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/forum/comments/{TestConstants.TestCommentId}/likes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Remove like from non-existent comment should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteForumCommentLike_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/forum/comments/{nonExistentId}/likes");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
