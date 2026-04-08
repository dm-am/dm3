using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// Integration tests for ChatController
/// </summary>
public class ChatControllerShould : IntegrationTestBase
{
    public ChatControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetChats Tests

    /// <summary>
    /// Get chats without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetChats_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/chats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get chats with query parameters should require authentication
    /// </summary>
    [Fact]
    public async Task GetChats_WithQueryParameters_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/chats?size=10&number=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetOrCreateDirectChat Tests

    /// <summary>
    /// Get or create direct chat by username without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetOrCreateDirectChat_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act - POST is used to get or create chat
        var response = await Client.PostAsync($"/v1/chats/direct/{TestConstants.SecondUserUsername}", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get or create direct chat with non-existent username should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetOrCreateDirectChat_WithNonExistentUsername_ReturnsUnauthorized()
    {
        // Act - POST is used to get or create chat
        var response = await Client.PostAsync("/v1/chats/direct/nonexistentuser", null);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetChat Tests

    /// <summary>
    /// Get chat without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetChat_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/chats/{TestConstants.TestChatId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get non-existent chat should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetChat_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/chats/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get chat with non-GUID string returns Unauthorized (route accepts string, auth first)
    /// </summary>
    [Fact]
    public async Task GetChat_WithNonGuidString_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/chats/not-a-guid");

        // Assert - route matches (accepts string id), auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region MarkAsRead Tests

    /// <summary>
    /// Mark messages as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task MarkAsRead_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act - DELETE is used to mark messages as read (unread pattern)
        var response = await Client.DeleteAsync($"/v1/chats/{TestConstants.TestChatId}/messages/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mark messages as read for non-existent chat should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task MarkAsRead_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act - DELETE is used to mark messages as read (unread pattern)
        var response = await Client.DeleteAsync($"/v1/chats/{nonExistentId}/messages/unread");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region CreateChat Tests

    /// <summary>
    /// Create chat without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task CreateChat_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var chat = new
        {
            title = "Test Group Chat",
            participants = new[] { TestConstants.SecondUserUsername }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/chats", chat);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create chat with empty title should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task CreateChat_WithEmptyTitle_ReturnsUnauthorized()
    {
        // Arrange
        var chat = new
        {
            title = "",
            participants = new[] { TestConstants.SecondUserUsername }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/chats", chat);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create chat with invalid participant should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task CreateChat_WithInvalidParticipant_ReturnsUnauthorized()
    {
        // Arrange
        var chat = new
        {
            title = "Test Group Chat",
            participants = new[] { "nonexistentuser12345" }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/chats", chat);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateChat Tests

    /// <summary>
    /// Update chat without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task UpdateChat_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/chats/{TestConstants.TestChatId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update non-existent chat should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task UpdateChat_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/chats/{nonExistentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update chat with non-GUID string returns Unauthorized (route accepts string, auth first)
    /// </summary>
    [Fact]
    public async Task UpdateChat_WithNonGuidString_ReturnsUnauthorized()
    {
        // Arrange
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/chats/not-a-guid")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - route matches (accepts string id), auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
