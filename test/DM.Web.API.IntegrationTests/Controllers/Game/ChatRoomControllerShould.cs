using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Integration tests for ChatRoomController
/// </summary>
public class ChatRoomControllerShould : IntegrationTestBase
{
    public ChatRoomControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetChatRooms Tests

    /// <summary>
    /// Get chat rooms without authentication should return OK (public endpoint)
    /// </summary>
    [Fact]
    public async Task GetChatRooms_WhenNotAuthenticated_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync($"/v1/games/{TestConstants.TestGameId}/chat-rooms");

        // Assert - public endpoint
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region GetChatRoom Tests

    /// <summary>
    /// Get chat room with non-existent ID should return NotFound
    /// </summary>
    [Fact]
    public async Task GetChatRoom_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/chat-rooms/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region CreateChatRoom Tests

    /// <summary>
    /// Create chat room without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task CreateChatRoom_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var chatRoom = new { title = "Test Chat Room" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/v1/games/{TestConstants.TestGameId}/chat-rooms", chatRoom);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateChatRoom Tests

    /// <summary>
    /// Update chat room without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task UpdateChatRoom_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var update = new { title = "Updated Title" };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/chat-rooms/{nonExistentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region DeleteChatRoom Tests

    /// <summary>
    /// Delete chat room without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task DeleteChatRoom_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/chat-rooms/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetChatRoomMessages Tests

    /// <summary>
    /// Get chat room messages without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetChatRoomMessages_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/chat-rooms/{nonExistentId}/messages");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region PostChatRoomMessage Tests

    /// <summary>
    /// Post chat room message without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostChatRoomMessage_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var message = new { text = "Test message" };

        // Act
        var response = await Client.PostAsJsonAsync(
            $"/v1/chat-rooms/{nonExistentId}/messages", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region MarkChatRoomAsRead Tests

    /// <summary>
    /// Mark chat room as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task MarkChatRoomAsRead_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/chat-rooms/{nonExistentId}/messages/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
