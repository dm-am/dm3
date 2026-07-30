using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.Entities.Messaging;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;

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
    /// A chat room of TestGame, whose master is TestUser, with its chat attached
    /// and deliberately without a single UserChatLink - that is exactly the shape
    /// the application creates.
    /// </summary>
    private async Task<(Guid RoomId, Guid ChatId)> CreateChatRoom()
    {
        var roomId = Guid.NewGuid();
        var chatId = Guid.NewGuid();

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        dbContext.Set<DbRoom>().Add(new DbRoom
        {
            RoomId = roomId,
            GameId = TestConstants.TestGameId,
            RoomNumber = 0,
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

        await dbContext.SaveChangesAsync();
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
