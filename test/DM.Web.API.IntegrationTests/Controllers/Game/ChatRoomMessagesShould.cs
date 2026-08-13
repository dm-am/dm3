using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A game room chat is authorized by the room it belongs to. It is created with
/// no participant rows at all, so the moment the messaging module gets to apply
/// its participation rule to it, every request is refused - the master of the
/// game included, who cannot possibly be a stranger to his own room.
///
/// The neighbouring file covers the authorization attributes only: all of its
/// facts are unauthenticated, so both halves of this - the room saying yes and
/// the chat saying no - stayed invisible to CI.
/// </summary>
public class ChatRoomMessagesShould : IntegrationTestBase
{
    private const string SeededText = "Chat room message from the fixture.";

    public ChatRoomMessagesShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task LetTheGameMasterReadItsOwnChatRoom()
    {
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/chat-rooms/{roomId}/messages");
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain(SeededText);
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    [Fact]
    public async Task LetTheGameMasterPostIntoItsOwnChatRoom()
    {
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/chat-rooms/{roomId}/messages");
            request.Content = JsonContent.Create(new { text = "Posted by the master." });

            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    /// <summary>
    /// The other half of the same rule. A game room chat is not one of the user's
    /// own chats, and the general chat endpoint has to keep refusing it. Making
    /// the room readable by loosening the participation predicate instead would
    /// open this door as a side effect, for everyone the room is available to.
    /// </summary>
    [Fact]
    public async Task KeepTheGameRoomChatOutOfTheGeneralChatEndpoint()
    {
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/chats/{chatId}");
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    /// <summary>
    /// A message written by one person is unread for the next, and reading the
    /// room clears it.
    /// </summary>
    /// <remarks>
    /// The room and the chat are the same thing under two identifiers, and the
    /// unread marker exists under the room's: the room half creates it, the game
    /// badge sums it and deleting the room removes it. Counting messages under
    /// the chat's identifier wrote into a marker nobody had made, so the number
    /// was zero before the message, zero after it, and zero forever.
    ///
    /// Stated end to end because that is the only place the two halves meet: each
    /// module is self-consistent, and every unit test of either one passed while
    /// the feature did nothing at all.
    /// </remarks>
    [Fact]
    public async Task CountAMessageAsUnreadForTheOtherReaderAndClearItOnRead()
    {
        var (roomId, chatId) = await CreateChatRoom(readerUserId: TestConstants.SecondUserId);
        try
        {
            var reader = new GeneralUser
            {
                UserId = TestConstants.SecondUserId,
                Username = TestConstants.SecondUserUsername,
                Role = UserRole.RegularUser,
                AccessPolicy = AccessPolicy.NotSpecified
            };

            await MarkAsRead(roomId, reader);
            (await UnreadCount(roomId, reader)).Should().Be(0, "the reader has just opened the room");

            var write = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/chat-rooms/{roomId}/messages");
            write.Content = JsonContent.Create(new { text = "Written while the other one was away." });
            (await Client.SendAsync(write)).StatusCode.Should().Be(HttpStatusCode.Created);

            (await UnreadCount(roomId, reader)).Should().Be(1, "one message arrived after the last read");

            await MarkAsRead(roomId, reader);
            (await UnreadCount(roomId, reader)).Should().Be(0, "the reader has read it");
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    /// <summary>
    /// The author is not told about their own message.
    /// </summary>
    [Fact]
    public async Task NotCountAMessageAsUnreadForItsOwnAuthor()
    {
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            var master = CustomWebApplicationFactory.CreateTestUser();
            await MarkAsRead(roomId, master);

            var write = CreateAuthenticatedRequest(HttpMethod.Post, $"/v1/chat-rooms/{roomId}/messages");
            write.Content = JsonContent.Create(new { text = "Written by the master." });
            (await Client.SendAsync(write)).StatusCode.Should().Be(HttpStatusCode.Created);

            (await UnreadCount(roomId, master)).Should().Be(0);
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    private async Task MarkAsRead(Guid roomId, GeneralUser user)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/chat-rooms/{roomId}/messages/unread", user);
        (await Client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private async Task<int> UnreadCount(Guid roomId, GeneralUser user)
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/games/{TestConstants.TestGameId}/chat-rooms", user);
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var listing = await response.Content.ReadFromJsonAsync<JsonElement>();
        return listing.GetProperty("resources")
            .EnumerateArray()
            .Single(r => r.GetProperty("id").GetGuid() == roomId)
            .GetProperty("unreadCount")
            .GetInt32();
    }

    /// <summary>
    /// A chat room of TestGame, whose master is TestUser, with its chat attached
    /// and deliberately without a single UserChatLink - that is exactly the shape
    /// the application creates. The unread marker is the one RoomService writes
    /// on creation: keyed by the room, parented by the game.
    /// </summary>
    private async Task<(Guid RoomId, Guid ChatId)> CreateChatRoom(Guid? readerUserId = null)
    {
        var roomId = Guid.NewGuid();
        var chatId = Guid.NewGuid();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = TestConstants.TestGameId,
            // Room numbers start at one and are unique within their game; the
            // fixture's own room of TestGame holds 1.
            RoomNumber = 2,
            Title = "OOC chat room",
            AccessType = RoomAccessType.Private,
            Type = RoomType.Chat,
            OrderNumber = 100.0,
            ViewPrivateText = false,
            ViewDiceResults = false,
            DiceEnabled = false,
            ChatId = chatId,
            IsRemoved = false
        });

        dbContext.Chats.Add(new Chat
        {
            ChatId = chatId,
            Type = ChatType.GameRoom,
            Title = "OOC chat room",
            RoomId = roomId,
            PublicId = $"r{chatId:N}"[..10]
        });

        dbContext.Messages.Add(new DbMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            UserId = TestConstants.TestUserId,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            Text = SeededText,
            IsRemoved = false
        });

        if (readerUserId.HasValue)
        {
            dbContext.Set<DbRoomAccess>().Add(new DbRoomAccess
            {
                AccessId = Guid.NewGuid(),
                RoomId = roomId,
                ReaderUserId = readerUserId.Value,
                Policy = RoomAccessPolicy.ReadOnly
            });
        }

        await dbContext.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IUnreadCountersRepository>()
            .CreateMarkerAsync(roomId, TestConstants.TestGameId, UnreadEntryType.Message);

        return (roomId, chatId);
    }

    private async Task DropChatRoom(Guid roomId, Guid chatId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(
            """UPDATE "Chats" SET "LastMessageId" = NULL WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Messages" WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Chats" WHERE "ChatId" = {0}""", chatId);
        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Rooms" WHERE "RoomId" = {0}""", roomId);
    }
}
