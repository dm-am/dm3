using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

/// <summary>
/// Integration tests for ConversationController
/// </summary>
public class ConversationControllerTests : IntegrationTestBase
{
    public ConversationControllerTests(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    #region GetConversations Tests

    /// <summary>
    /// Get conversations without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetConversations_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/conversations");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get conversations with query parameters should require authentication
    /// </summary>
    [Fact]
    public async Task GetConversations_WithQueryParameters_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/conversations?size=10&number=1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetDirectConversationById Tests

    /// <summary>
    /// Get direct conversation by user ID without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetDirectConversationById_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/conversations/direct/{TestConstants.SecondUserId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get direct conversation with non-existent user should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetDirectConversationById_WithNonExistentUserId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/conversations/direct/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get direct conversation with invalid GUID should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetDirectConversationById_WithInvalidGuid_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/conversations/direct/not-a-guid");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetDirectConversation Tests

    /// <summary>
    /// Get direct conversation by login without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetDirectConversation_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/conversations/direct/{TestConstants.SecondUserLogin}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get direct conversation with non-existent login should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetDirectConversation_WithNonExistentLogin_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/conversations/direct/nonexistentuser");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region GetConversation Tests

    /// <summary>
    /// Get conversation without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetConversation_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync($"/v1/conversations/{TestConstants.TestConversationId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get non-existent conversation should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetConversation_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.GetAsync($"/v1/conversations/{nonExistentId}");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Get conversation with invalid GUID should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task GetConversation_WithInvalidGuid_ReturnsUnauthorized()
    {
        // Act
        var response = await Client.GetAsync("/v1/conversations/not-a-guid");

        // Assert - auth check happens first
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
        // Act
        var response = await Client.DeleteAsync($"/v1/conversations/{TestConstants.TestConversationId}/messages/unread");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Mark messages as read for non-existent conversation should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task MarkAsRead_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/conversations/{nonExistentId}/messages/unread");

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region CreateConversation Tests

    /// <summary>
    /// Create conversation without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task CreateConversation_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var conversation = new
        {
            title = "Test Group Chat",
            participants = new[] { TestConstants.SecondUserLogin }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/conversations", conversation);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create conversation with empty title should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task CreateConversation_WithEmptyTitle_ReturnsUnauthorized()
    {
        // Arrange
        var conversation = new
        {
            title = "",
            participants = new[] { TestConstants.SecondUserLogin }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/conversations", conversation);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Create conversation with invalid participant should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task CreateConversation_WithInvalidParticipant_ReturnsUnauthorized()
    {
        // Arrange
        var conversation = new
        {
            title = "Test Group Chat",
            participants = new[] { "nonexistentuser12345" }
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/conversations", conversation);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region UpdateConversation Tests

    /// <summary>
    /// Update conversation without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task UpdateConversation_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/conversations/{TestConstants.TestConversationId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update non-existent conversation should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task UpdateConversation_WithNonExistentId_ReturnsUnauthorized()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, $"/v1/conversations/{nonExistentId}")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Update conversation with invalid GUID should return Unauthorized (auth check first)
    /// </summary>
    [Fact]
    public async Task UpdateConversation_WithInvalidGuid_ReturnsUnauthorized()
    {
        // Arrange
        var update = new
        {
            title = "Updated Title"
        };
        var content = JsonContent.Create(update);
        var request = new HttpRequestMessage(HttpMethod.Patch, "/v1/conversations/not-a-guid")
        {
            Content = content
        };

        // Act
        var response = await Client.SendAsync(request);

        // Assert - auth check happens first
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}
