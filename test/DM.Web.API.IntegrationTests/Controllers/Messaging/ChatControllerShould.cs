using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// GET /chats/{id} accepts a readable id as well as a GUID, and the global chat
    /// is the one chat addressable by name.
    /// </summary>
    /// <remarks>
    /// The branch used to be unreachable: nothing assigned Chat.PublicId, so every
    /// non-GUID id resolved to nothing at all.
    /// </remarks>
    [Fact]
    public async Task ResolveTheGlobalChatByItsReadableId()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/chats/global");
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);
        // The readable id resolved to the right row. It is not echoed back: the chat
        // DTO carries no public id field, which is why the branch could stay broken
        // unnoticed for so long - nothing on the wire ever showed it.
        content.Should().Contain("00000000-0000-0000-0000-000000000001");
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

    #region GetUnreadMessagesCount Tests

    /// <summary>
    /// The unread total is one person's number and nobody else's.
    /// </summary>
    [Fact]
    public async Task RefuseTheUnreadCountToAStranger()
    {
        var response = await Client.GetAsync("/v1/chats/unread");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The header badge asks this endpoint for the whole total, and the whole
    /// total is what the markers hold: every conversation of the reader, not the
    /// page of them the chat list happens to return.
    /// </summary>
    /// <remarks>
    /// Asserted as a delta rather than an absolute number: the fixture is shared,
    /// so what the reader already has unread is whatever ran before this.
    /// The marker is written straight to the store because the endpoint's whole
    /// subject is the aggregate over it, and the conversation the marker belongs
    /// to does not have to exist for the sum to be asked for — the counter
    /// carries no foreign key by design.
    /// </remarks>
    [Fact]
    public async Task SumTheUnreadMarkersOfEveryConversationOfTheReader()
    {
        var before = await ReadUnreadCount();

        var entityId = Guid.NewGuid();
        var strangerEntityId = Guid.NewGuid();
        var tombstoneEntityId = Guid.NewGuid();
        var underAnotherParentId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Set<UnreadCounter>().AddRange(
                new UnreadCounter
                {
                    UserId = TestConstants.TestUserId,
                    EntityId = entityId,
                    // A conversation is parented by its reader, which is what makes
                    // the total answerable in one query.
                    ParentId = TestConstants.TestUserId,
                    EntryType = UnreadEntryType.Message,
                    LastReadUtc = DateTime.UtcNow,
                    Counter = 7,
                    IsRemoved = false
                },
                // Somebody else's conversation, parented under him. The badge is
                // the reader's own, and a total that summed the table would put
                // every other member's unread messages on it.
                new UnreadCounter
                {
                    UserId = strangerId,
                    EntityId = strangerEntityId,
                    ParentId = strangerId,
                    EntryType = UnreadEntryType.Message,
                    LastReadUtc = DateTime.UtcNow,
                    Counter = 5,
                    IsRemoved = false
                },
                // The reader's own unread messages somewhere that is not a
                // conversation: a forum topic carries the same entry type and is
                // parented by its board. This badge counts conversations, and the
                // parent is the whole of what tells the two apart.
                new UnreadCounter
                {
                    UserId = TestConstants.TestUserId,
                    EntityId = underAnotherParentId,
                    ParentId = boardId,
                    EntryType = UnreadEntryType.Message,
                    LastReadUtc = DateTime.UtcNow,
                    Counter = 5,
                    IsRemoved = false
                },
                // A marker of the reader's own that was taken back: he left the
                // conversation, or it was deleted. The row stays, because an upsert
                // over it revives the marker if he is added again, and a total that
                // counted it would show a badge for a conversation he cannot open.
                new UnreadCounter
                {
                    UserId = TestConstants.TestUserId,
                    EntityId = tombstoneEntityId,
                    ParentId = TestConstants.TestUserId,
                    EntryType = UnreadEntryType.Message,
                    LastReadUtc = DateTime.UtcNow,
                    Counter = 5,
                    IsRemoved = true,
                    RemovedUtc = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        try
        {
            var after = await ReadUnreadCount();

            (after - before).Should().Be(7,
                "the endpoint answers with the sum of the reader's own live conversation " +
                "markers: not a stranger's conversation, not his unread messages elsewhere, " +
                "and not a marker he no longer holds");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Set<UnreadCounter>()
                .Where(c => c.EntityId == entityId ||
                            c.EntityId == strangerEntityId ||
                            c.EntityId == underAnotherParentId ||
                            c.EntityId == tombstoneEntityId)
                .ExecuteDeleteAsync();
        }
    }

    private async Task<long> ReadUnreadCount()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/chats/unread");
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);

        using var document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("resource").GetProperty("count").GetInt64();
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
