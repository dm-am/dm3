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

// A game room chat is authorized by the room it belongs to: it has no participant rows, so the
// chat's own participation rule would refuse its own master.
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

    // A room is not moderated, so a removed message of its chat reaches nobody: not the master who
    // reads the room, not a moderator.
    [Fact]
    public async Task GiveTheTextOfARemovedMessageToNobody()
    {
        const string removedText = "Removed chat room message.";
        var (roomId, chatId) = await CreateChatRoom();
        try
        {
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();
                dbContext.Messages.Add(new DbMessage
                {
                    MessageId = Guid.NewGuid(),
                    ChatId = chatId,
                    UserId = TestConstants.TestUserId,
                    CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
                    Text = removedText,
                    IsRemoved = true,
                    DeletedByUserId = TestConstants.TestUserId
                });
                await dbContext.SaveChangesAsync();
            }

            var asMaster = await Client.SendAsync(
                CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/chat-rooms/{roomId}/messages"));
            var asModerator = await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/chat-rooms/{roomId}/messages", CustomWebApplicationFactory.CreateModeratorUser()));

            var masterBody = await asMaster.Content.ReadAsStringAsync();
            masterBody.Should().Contain(SeededText).And.NotContain(removedText);
            (await asModerator.Content.ReadAsStringAsync()).Should().NotContain(removedText);
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

    /// <remarks>
    /// The room and the chat are one thing under two identifiers, and the marker is the room's. End to end: each
    /// module is self-consistent on its own.
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
                Username = TestConstants.SecondUserLogin,
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
    /// The open arm lets every reader in, and only GetChatRoomAsync through RoomAvailable turns away a game the
    /// reader does not see; the resolver tests cannot see that read.
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

    // The status and body a guest gets, minus the trace and with identifiers written alike.
    private async Task<string> GuestAnswer(Guid roomId)
    {
        var response = await Client.GetAsync($"/v1/chat-rooms/{roomId}/messages");
        return $"{(int)response.StatusCode} " + Masked(await response.Content.ReadAsStringAsync());
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

    // A chat room of TestGame without a single UserChatLink, as the application creates it, with
    // the room's marker.
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
