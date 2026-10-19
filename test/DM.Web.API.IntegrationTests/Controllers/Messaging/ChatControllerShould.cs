using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Messaging;

public class ChatControllerShould : IntegrationTestBase
{
    public ChatControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// GET /chats/{id} accepts a readable id as well as a GUID, and the global chat
    /// is the one chat addressable by name.
    /// </summary>
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

    /// <summary>
    /// Get chats without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetChatsWhenNotAuthenticatedReturnsUnauthorized()
    {
        var response = await Client.GetAsync("/v1/chats");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

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
    /// The header badge asks for the whole total the markers hold, not the page the chat list returns.
    /// </summary>
    /// <remarks>Asserted as a delta over a marker written straight to the store; the counter carries no foreign key.</remarks>
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
                // A conversation is parented by its reader, which is what makes
                // the total answerable in one query.
                Marker(TestConstants.TestUserId, entityId, TestConstants.TestUserId, 7),
                // Somebody else's conversation, parented under him. The badge is
                // the reader's own, and a total that summed the table would put
                // every other member's unread messages on it.
                Marker(strangerId, strangerEntityId, strangerId, 5),
                // The reader's own unread messages somewhere that is not a
                // conversation: a forum topic carries the same entry type and is
                // parented by its board. This badge counts conversations, and the
                // parent is the whole of what tells the two apart.
                Marker(TestConstants.TestUserId, underAnotherParentId, boardId, 5),
                // A marker of the reader's own that was taken back: he left the
                // conversation, or it was deleted. The row stays, because an upsert
                // over it revives the marker if he is added again, and a total that
                // counted it would show a badge for a conversation he cannot open.
                Marker(TestConstants.TestUserId, tombstoneEntityId, TestConstants.TestUserId, 5, removed: true));
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

    /// <summary>The unread marker of messages a reader has under one parent.</summary>
    private static UnreadCounter Marker(Guid userId, Guid entityId, Guid parentId, int counter, bool removed = false) => new()
    {
        UserId = userId,
        EntityId = entityId,
        ParentId = parentId,
        EntryType = UnreadEntryType.Message,
        LastReadUtc = DateTime.UtcNow,
        Counter = counter,
        IsRemoved = removed,
        RemovedUtc = removed ? DateTime.UtcNow : null
    };

    private async Task<long> ReadUnreadCount()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/chats/unread");
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);

        using var document = JsonDocument.Parse(content);
        return document.RootElement.GetProperty("resource").GetProperty("count").GetInt64();
    }

    /// <summary>
    /// Get chat without authentication should return Unauthorized
    /// </summary>
    [Fact]
    public async Task GetChatWhenNotAuthenticatedReturnsUnauthorized()
    {
        var response = await Client.GetAsync($"/v1/chats/{TestConstants.TestChatId}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

}
