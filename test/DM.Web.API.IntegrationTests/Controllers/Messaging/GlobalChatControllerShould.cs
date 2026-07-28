using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// Integration tests for GlobalChatController
/// </summary>
public class GlobalChatControllerShould : IntegrationTestBase
{
    public GlobalChatControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region PostGlobalChatMessage Tests

    /// <summary>
    /// Post global chat message without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task PostGlobalChatMessage_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var message = new { text = "Hello global chat!" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/global-chat/messages", message);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Post global chat message with empty text should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task PostGlobalChatMessage_WithEmptyText_ReturnsUnauthorized()
    {
        // Arrange
        var message = new { text = "" };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/global-chat/messages", message);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region MarkGlobalChatMessagesAsRead Tests

    /// <summary>
    /// Mark global chat messages as read without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task MarkGlobalChatMessagesAsRead_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.DeleteAsync("/v1/global-chat/messages/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GlobalChatEvents Tests

    /// <summary>
    /// Get global chat events should return OK (public endpoint)
    /// </summary>
    [Fact]
    public async Task GetGlobalChatEvents_WhenNotAuthenticated_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/v1/global-chat/events");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Get active global chat event should return OK or NoContent (depending on whether an event is active)
    /// </summary>
    [Fact]
    public async Task GetActiveGlobalChatEvent_WhenNoneActive_ReturnsNoContent()
    {
        // Act
        var response = await Client.GetAsync("/v1/global-chat/events/active");

        // Assert - nothing in the fixture opens an event
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Get global chat event by id should return NotFound for non-existent event
    /// </summary>
    [Fact]
    public async Task GetGlobalChatEvent_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/global-chat/events/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Create global chat event without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task CreateGlobalChatEvent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var eventData = new
        {
            title = "Test Event",
            startsUtc = DateTimeOffset.UtcNow.AddHours(1).ToString("o"),
            isOpen = true
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/global-chat/events", eventData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Join global chat event without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task JoinGlobalChatEvent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/global-chat/events/{nonExistentId}/join", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Leave global chat event without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task LeaveGlobalChatEvent_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.PostAsync($"/v1/global-chat/events/{nonExistentId}/leave", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
