using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.UnreadCounters;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Messaging;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Web.API.IntegrationTests.Helpers;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

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

    /// <summary>
    /// An open chat room is read by everybody who sees its game, and by nobody
    /// who does not.
    /// </summary>
    /// <remarks>
    /// The rule of the room does not ask about the game: the open arm of
    /// ViewMessages lets every reader in. What turns away the game a reader does
    /// not see is the read that brings the room to the rule, GetChatRoomAsync
    /// through RoomAvailable, and nothing else stands between a stranger and the
    /// history of an open chat room of a private draft or of a game that waits on
    /// premoderation. The unit tests of the resolver cannot see that read, and
    /// the scope check of the repositories only asks that the user is passed,
    /// not that the filter is right, so the answer is stated here end to end:
    /// the same room reads while its game is in sight and is absent once the
    /// game is hidden.
    /// </remarks>
    [Theory]
    [InlineData(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.AwaitingEdits, DraftVisibility.Public)]
    public async Task KeepTheOpenChatRoomOfAGameAStrangerDoesNotSeeShut(
        ModuleStatus hiddenStatus,
        PremoderationStatus hiddenPremoderation,
        DraftVisibility hiddenVisibility)
    {
        var (gameId, roomId) = await CreateGameWithOpenChatRoom();
        var stranger = await CreateStranger();
        var url = $"/v1/chat-rooms/{roomId}/messages";

        var whileSeen = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, stranger));
        whileSeen.StatusCode.Should().Be(HttpStatusCode.OK,
            "an open chat room is read by everybody who sees the game");
        (await whileSeen.Content.ReadAsStringAsync()).Should().Contain(SeededText);

        await HideGame(gameId, hiddenStatus, hiddenPremoderation, hiddenVisibility);

        var whileHidden = await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, stranger));
        whileHidden.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a game the stranger does not see takes its open chat room out of his reach");
    }

    /// <summary>
    /// A guest reads an open chat room the way he reads the posts of an open room:
    /// everybody who sees the game reads it, and a guest sees a public game. Once
    /// the game is out of his sight, the room answers him as an address with
    /// nothing behind it, word for word.
    /// </summary>
    [Theory]
    [InlineData(ModuleStatus.Draft, PremoderationStatus.Approved, DraftVisibility.Private)]
    [InlineData(ModuleStatus.Active, PremoderationStatus.AwaitingEdits, DraftVisibility.Public)]
    public async Task LetAGuestReadTheOpenChatRoomOfAGameHeSeesAndNoOther(
        ModuleStatus hiddenStatus,
        PremoderationStatus hiddenPremoderation,
        DraftVisibility hiddenVisibility)
    {
        var (gameId, roomId) = await CreateGameWithOpenChatRoom();

        var whileSeen = await Client.GetAsync($"/v1/chat-rooms/{roomId}/messages");
        whileSeen.StatusCode.Should().Be(HttpStatusCode.OK,
            "an open chat room is read by everybody who sees the game, a guest included");
        (await whileSeen.Content.ReadAsStringAsync()).Should().Contain(SeededText);

        await HideGame(gameId, hiddenStatus, hiddenPremoderation, hiddenVisibility);

        (await GuestAnswer(roomId)).Should().Be(await GuestAnswer(Guid.NewGuid()),
            "the open chat room of a game the guest does not see answers him as nothing");
    }

    /// <summary>
    /// A closed chat room is read by the people it was opened to, all of them
    /// signed in, and a guest is told nothing about it: not a refusal, which would
    /// confirm the room exists, but the answer for an address with nothing behind
    /// it. The same room opened up is read by the same guest, so it is the room
    /// being closed that turns him away, not the game.
    /// </summary>
    [Fact]
    public async Task AnswerAGuestAboutAClosedChatRoomAsAboutNothing()
    {
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            (await GuestAnswer(roomId)).Should().Be(await GuestAnswer(Guid.NewGuid()),
                "a closed chat room is no business of a guest, down to whether it exists");

            await OpenRoom(roomId);

            var opened = await Client.GetAsync($"/v1/chat-rooms/{roomId}/messages");
            opened.StatusCode.Should().Be(HttpStatusCode.OK,
                "the game the room is in is public, so the room opened up is the guest's to read");
            (await opened.Content.ReadAsStringAsync()).Should().Contain(SeededText);
        }
        finally
        {
            await DropChatRoom(roomId, chatId);
        }
    }

    /// <summary>
    /// What a guest is answered for the messages of a chat room: the status and the
    /// body, minus the per-request trace and with identifiers written alike, so a
    /// sentence that only repeats the id it was given is not told apart.
    /// </summary>
    private async Task<string> GuestAnswer(Guid roomId)
    {
        var response = await Client.GetAsync($"/v1/chat-rooms/{roomId}/messages");
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var fields = string.Join("&", document.RootElement
            .EnumerateObject()
            .Where(p => p.Name != "traceId")
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"{p.Name}={p.Value.GetRawText()}"));
        return $"{(int)response.StatusCode} " + System.Text.RegularExpressions.Regex.Replace(fields,
            @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", "{id}");
    }

    private async Task OpenRoom(Guid roomId)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var room = await dbContext.Set<DbRoom>().SingleAsync(r => r.RoomId == roomId);
        room.AccessType = RoomAccessType.Open;
        await dbContext.SaveChangesAsync();
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

    /// <summary>
    /// A game of its own, public and approved, mastered by TestUser, with an open
    /// chat room holding one message. Fresh identifiers every time: the fixture
    /// database is shared and the public ids are uniquely indexed, and a game of
    /// its own is what the test may hide without touching anybody else's.
    /// </summary>
    private async Task<(Guid GameId, Guid RoomId)> CreateGameWithOpenChatRoom()
    {
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();
        var chatId = Guid.NewGuid();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = TestConstants.TestUserId,
            Title = "Open chat room game",
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Public,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        });
        dbContext.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(gameId));

        dbContext.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Open OOC chat room",
            AccessType = RoomAccessType.Open,
            Type = RoomType.Chat,
            OrderNumber = 1.0,
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
            Title = "Open OOC chat room",
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

        await dbContext.SaveChangesAsync();
        return (gameId, roomId);
    }

    /// <summary>A signed-in reader with no role in any game.</summary>
    private async Task<GeneralUser> CreateStranger()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var userId = Guid.NewGuid();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"str{userId:N}"[..20],
            Email = $"{userId:N}@chatroom.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return new GeneralUser { UserId = user.UserId, Username = user.Username, Role = UserRole.RegularUser };
    }

    private async Task HideGame(
        Guid gameId, ModuleStatus status, PremoderationStatus premoderation, DraftVisibility visibility)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var game = await dbContext.Games.SingleAsync(g => g.GameId == gameId);
        game.Status = status;
        game.PremoderationStatus = premoderation;
        game.DraftVisibility = visibility;
        await dbContext.SaveChangesAsync();
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
