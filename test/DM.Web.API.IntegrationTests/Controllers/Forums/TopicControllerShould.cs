using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Forums;

/// <summary>
/// Integration tests for TopicController
/// </summary>
public class TopicControllerShould : IntegrationTestBase
{
    public TopicControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetBoardTopics Tests

    /// <summary>
    /// Get topics from valid board should return OK
    /// </summary>
    [Fact]
    public async Task GetBoardTopics_WithValidBoard_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/boards/{Uri.EscapeDataString(TestConstants.TestBoardTitle)}/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resources");
    }

    /// <summary>
    /// Get topics from non-existent board should return Gone
    /// </summary>
    [Fact]
    public async Task GetBoardTopics_WithNonExistentBoard_ReturnsGone()
    {
        // Arrange
        var nonExistentBoardId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/boards/{nonExistentBoardId}/topics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get topics with invalid board id should return error
    /// </summary>
    [Fact]
    public async Task GetBoardTopics_WithInvalidBoardId_ReturnsError()
    {
        // Act
        var response = await Client.GetAsync("/v1/boards/not-a-guid/topics");

        // Assert - invalid GUID should return 410 Gone (topic/board not found)
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    #endregion

    #region PostBoardTopic Tests

    /// <summary>
    /// Create topic without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostBoardTopic_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var topic = new
        {
            title = "Test Topic",
            text = "Test topic content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/boards/{Uri.EscapeDataString(TestConstants.TestBoardTitle)}/topics", topic);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create topic with empty title should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostBoardTopic_WithEmptyTitle_ReturnsUnauthorized()
    {
        // Arrange
        var topic = new
        {
            title = "",
            text = "Test topic content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/boards/{Uri.EscapeDataString(TestConstants.TestBoardTitle)}/topics", topic);

        // Assert - auth check happens before validation
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create topic on non-existent board should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostBoardTopic_OnNonExistentBoard_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentBoardId = Guid.NewGuid();
        var topic = new
        {
            title = "Test Topic",
            text = "Test topic content"
        };

        // Act
        var response = await Client.PostAsJsonAsync($"/v1/boards/{nonExistentBoardId}/topics", topic);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetTopic Tests

    /// <summary>
    /// Get existing topic should return OK
    /// </summary>
    [Fact]
    public async Task GetTopic_WithValidId_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/topics/{TestConstants.TestTopicId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("resource");
    }

    /// <summary>
    /// Get non-existent topic should return Gone
    /// </summary>
    [Fact]
    public async Task GetTopic_WithNonExistentId_ReturnsGone()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/topics/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    /// <summary>
    /// Get topic with invalid GUID should return error
    /// </summary>
    [Fact]
    public async Task GetTopic_WithInvalidGuid_ReturnsError()
    {
        // Act
        var response = await Client.GetAsync("/v1/topics/not-a-guid");

        // Assert - invalid GUID in route returns 404 Not Found (route not matched) or 400
        response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.BadRequest);
    }

    #endregion

    #region PatchTopic Tests

    /// <summary>
    /// Update topic without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PatchTopic_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var topicUpdate = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(topicUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/topics/{TestConstants.TestTopicId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PatchTopic_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var topicUpdate = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(topicUpdate);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/topics/{nonExistentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteTopic Tests

    /// <summary>
    /// Delete topic without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteTopic_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{TestConstants.TestTopicId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Delete non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteTopic_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PostTopicLike Tests

    /// <summary>
    /// Like topic without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostTopicLike_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.PostAsync($"/v1/topics/{TestConstants.TestTopicId}/likes", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Like non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostTopicLike_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/topics/{nonExistentId}/likes", null);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteTopicLike Tests

    /// <summary>
    /// Remove like without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteTopicLike_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{TestConstants.TestTopicId}/likes");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Remove like from non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task DeleteTopicLike_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{nonExistentId}/likes");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region ReadTopicComments Tests

    /// <summary>
    /// Mark comments as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task ReadTopicComments_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{TestConstants.TestTopicId}/comments/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mark comments as read for non-existent topic should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task ReadTopicComments_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/topics/{nonExistentId}/comments/unread");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
