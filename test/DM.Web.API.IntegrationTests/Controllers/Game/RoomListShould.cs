using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbRoomAccess = DM.Infrastructure.Persistence.Entities.Game.Links.RoomAccess;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// The list of the rooms of a game, the way the room settings and the game menu
/// read it: in the order the master put them in, with the number of messages in
/// each, and a closed room named to those who may not open it with the
/// characters let in to write in it.
/// </summary>
/// <remarks>
/// Through HTTP, because each of the three is a promise of the payload: the
/// order is written by one request and read by another, and the two fields
/// travel through the service, which clears a closed room, and the mapper.
/// </remarks>
public class RoomListShould : IntegrationTestBase
{
    public RoomListShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// A room moved to the head of the list is first in it. The move used to
    /// rewrite links nobody read while the list sorted by a number nobody
    /// wrote, so the answer was 200 and the order stayed as it was.
    /// </summary>
    [Fact]
    public async Task PutARoomMovedToTheHeadFirst()
    {
        var world = await Seed();
        try
        {
            (await Move(world, world.Chat, null)).Should().Be(HttpStatusCode.OK);

            (await Order(world)).Should().Equal(world.Chat, world.Hall, world.Closed);
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// Moves one after another: each starts from the order the one before it
    /// left, which is what the settings send for one "Сохранить".
    /// </summary>
    [Fact]
    public async Task PutARoomRightAfterTheRoomNamed()
    {
        var world = await Seed();
        try
        {
            (await Move(world, world.Hall, world.Closed)).Should().Be(HttpStatusCode.OK);
            (await Order(world)).Should().Equal(world.Closed, world.Hall, world.Chat);

            (await Move(world, world.Chat, world.Closed)).Should().Be(HttpStatusCode.OK);
            (await Order(world)).Should().Equal(world.Closed, world.Chat, world.Hall);
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// A room of another game is no place in this one: the move is refused by
    /// name and the order stays.
    /// </summary>
    [Fact]
    public async Task RefuseToPutARoomAfterARoomOfAnotherGame()
    {
        var world = await Seed();
        try
        {
            (await Move(world, world.Hall, world.Foreign)).Should().Be(HttpStatusCode.BadRequest);

            (await Order(world)).Should().Equal(world.Hall, world.Closed, world.Chat);
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// The posts of a room of posts and the messages of a room of chat, the
    /// removed ones left out.
    /// </summary>
    [Fact]
    public async Task CountTheMessagesOfEveryRoom()
    {
        var world = await Seed();
        try
        {
            var rooms = await Rooms(world, world.Master);

            Room(rooms, world.Hall).GetProperty("totalPostsCount").GetInt32().Should().Be(2);
            Room(rooms, world.Closed).GetProperty("totalPostsCount").GetInt32().Should().Be(1);
            Room(rooms, world.Chat).GetProperty("totalPostsCount").GetInt32().Should().Be(3);
        }
        finally
        {
            await Drop(world);
        }
    }

    /// <summary>
    /// "[Лес] - Густав, Эодор": a guest and a stranger see the closed room with
    /// the characters let in to write in it, in the order they joined the game,
    /// and nothing else of it. A dead character is named as the old site names
    /// him, a removed one is nobody. The character let in to read and the
    /// person let in by name are not named, and the master, who opens the room,
    /// reads its accesses instead.
    /// </summary>
    [Fact]
    public async Task NameTheWritersOfAClosedRoomToWhoeverMayNotOpenIt()
    {
        var world = await Seed();
        try
        {
            foreach (var caller in new[] { null, world.Stranger })
            {
                var closed = Room(await Rooms(world, caller), world.Closed);

                closed.GetProperty("canView").GetBoolean().Should().BeFalse();
                closed.GetProperty("characterNames").EnumerateArray().Select(n => n.GetString())
                    .Should().Equal(world.FirstWriter, world.SecondWriter, world.DeadWriter);
                closed.GetProperty("accesses").EnumerateArray().Should().BeEmpty();
                closed.GetProperty("totalPostsCount").GetInt32().Should().Be(0);
            }

            var forMaster = Room(await Rooms(world, world.Master), world.Closed);
            forMaster.GetProperty("canView").GetBoolean().Should().BeTrue();
            forMaster.GetProperty("characterNames").EnumerateArray().Should().BeEmpty();
            forMaster.GetProperty("accesses").EnumerateArray().Should().HaveCount(5,
                "the grant of the removed character goes with him");
        }
        finally
        {
            await Drop(world);
        }
    }

    // ═══ REQUESTS ═══

    private async Task<HttpStatusCode> Move(World world, Guid roomId, Guid? previousRoomId)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, $"/v1/rooms/{roomId}", world.Master);
        var previous = previousRoomId.HasValue ? $"\"{previousRoomId}\"" : "null";
        request.Content = new StringContent(
            $$"""{"previousRoomId":{{previous}}}""", Encoding.UTF8, "application/json");
        var response = await Client.SendAsync(request);
        return response.StatusCode;
    }

    private async Task<Guid[]> Order(World world) =>
        (await Rooms(world, world.Master)).Select(r => r.GetProperty("id").GetGuid()).ToArray();

    private async Task<JsonElement[]> Rooms(World world, GeneralUser? caller)
    {
        var url = $"/v1/games/{world.GameId}/rooms";
        var request = caller == null
            ? new HttpRequestMessage(HttpMethod.Get, url)
            : CreateAuthenticatedRequest(HttpMethod.Get, url, caller);
        var response = await Client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", content);

        return JsonDocument.Parse(content).RootElement.GetProperty("resources").EnumerateArray().ToArray();
    }

    private static JsonElement Room(JsonElement[] rooms, Guid id) =>
        rooms.Should().ContainSingle(r => r.GetProperty("id").GetGuid() == id).Subject;

    // ═══ SEEDING ═══

    private sealed record World(
        Guid GameId,
        Guid OtherGameId,
        Guid Hall,
        Guid Closed,
        Guid Chat,
        Guid Foreign,
        Guid ChatId,
        GeneralUser Master,
        GeneralUser Stranger,
        Guid[] UserIds,
        string FirstWriter,
        string SecondWriter,
        string DeadWriter);

    /// <summary>
    /// An open game with three rooms in the order hall, closed room, chat, and a
    /// second game of the same master with a room of its own. Two posts in the
    /// hall and one removed, one post in the closed room, three messages in the
    /// chat and one removed. In the closed room two characters write, a dead
    /// and a removed one were let in to write, one character reads, and one
    /// person is let in by name.
    /// </summary>
    private async Task<World> Seed()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var hall = Guid.NewGuid();
        var closed = Guid.NewGuid();
        var chat = Guid.NewGuid();
        var foreign = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var suffix = gameId.ToString("N")[..6];

        var master = NewUser("rlm");
        var stranger = NewUser("rls");
        var players = Enumerable.Range(0, 5).Select(_ => NewUser("rlp")).ToArray();
        var reader = NewUser("rlr");
        var firstWriter = $"Густав {suffix}";
        var secondWriter = $"Эодор {suffix}";
        var deadWriter = $"Николай {suffix}";

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        db.Users.AddRange(master, stranger, reader);
        db.Users.AddRange(players);
        db.Set<DbGame>().AddRange(NewGame(gameId, master.UserId, now), NewGame(otherGameId, master.UserId, now));

        db.Set<DbRoom>().AddRange(
            NewRoom(hall, gameId, 1, "Зала", RoomAccessType.Open, RoomType.Default, null),
            NewRoom(closed, gameId, 2, "Лес", RoomAccessType.Private, RoomType.Default, null),
            NewRoom(chat, gameId, 3, "Шепот", RoomAccessType.Open, RoomType.Chat, chatId),
            NewRoom(foreign, otherGameId, 1, "Чужая", RoomAccessType.Open, RoomType.Default, null));
        db.Chats.Add(new DbChat
        {
            ChatId = chatId,
            Type = ChatType.GameRoom,
            Title = "Шепот",
            RoomId = chat,
            PublicId = $"l{chatId:N}"[..10]
        });

        // Joined in the order the names are expected in, the second writer
        // added to the room first: the names follow the game, not the grants.
        var second = NewCharacter(gameId, players[1].UserId, secondWriter, now.AddDays(-2));
        var first = NewCharacter(gameId, players[0].UserId, firstWriter, now.AddDays(-3));
        var onlyReads = NewCharacter(gameId, players[2].UserId, $"Читающий {suffix}", now.AddDays(-4));
        var dead = NewCharacter(gameId, players[3].UserId, deadWriter, now.AddDays(-1));
        dead.Status = CharacterStatus.Retired;
        dead.IsDead = true;
        // Joined first: named at all, he would head the line.
        var removed = NewCharacter(gameId, players[4].UserId, $"Удаленный {suffix}", now.AddDays(-5));
        removed.IsRemoved = true;
        db.Set<DbCharacter>().AddRange(second, first, onlyReads, dead, removed);
        db.Set<DbRoomAccess>().AddRange(
            Grant(closed, removed.CharacterId, null, RoomAccessPolicy.Full),
            Grant(closed, dead.CharacterId, null, RoomAccessPolicy.Full),
            Grant(closed, second.CharacterId, null, RoomAccessPolicy.Full),
            Grant(closed, first.CharacterId, null, RoomAccessPolicy.Full),
            Grant(closed, onlyReads.CharacterId, null, RoomAccessPolicy.ReadOnly),
            Grant(closed, null, reader.UserId, RoomAccessPolicy.ReadOnly));

        db.Set<DbPost>().AddRange(
            NewPost(hall, first, now.AddHours(-3), removed: false),
            NewPost(hall, first, now.AddHours(-2), removed: false),
            NewPost(hall, first, now.AddHours(-1), removed: true),
            NewPost(closed, first, now.AddHours(-1), removed: false));
        db.Messages.AddRange(Enumerable.Range(0, 4).Select(i => new DbMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = chatId,
            UserId = players[0].UserId,
            CreatedUtc = now.AddMinutes(-10 + i),
            Text = $"Сообщение {i}",
            IsRemoved = i == 3
        }));

        await db.SaveChangesAsync();

        return new World(gameId, otherGameId, hall, closed, chat, foreign, chatId,
            Caller(master), Caller(stranger),
            [master.UserId, stranger.UserId, reader.UserId, .. players.Select(p => p.UserId)],
            firstWriter, secondWriter, deadWriter);
    }

    private static DbUser NewUser(string prefix)
    {
        var id = Guid.NewGuid();
        return new DbUser
        {
            UserId = id,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"{prefix}{id:N}"[..20],
            Email = $"{id:N}@roomlist.example",
            PasswordHash = "hash",
            Salt = "salt",
            Role = UserRole.RegularUser,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
            LastActivityUtc = DateTimeOffset.UtcNow
        };
    }

    private static GeneralUser Caller(DbUser user) =>
        new() { UserId = user.UserId, Username = user.Username, Role = UserRole.RegularUser };

    private static DbGame NewGame(Guid gameId, Guid masterId, DateTimeOffset now) => new()
    {
        GameId = gameId,
        PublicId = gameId.ToString("N")[..10],
        MasterId = masterId,
        Title = "Игра для списка комнат",
        CreatedUtc = now.AddDays(-10),
        ActivatedUtc = now.AddDays(-9),
        Status = ModuleStatus.Active,
        PremoderationStatus = PremoderationStatus.Approved,
        CommentsAccessMode = CommentsAccessMode.Public
    };

    private static DbRoom NewRoom(
        Guid roomId, Guid gameId, int number, string title, RoomAccessType access, RoomType type, Guid? chatId) => new()
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = number,
            Title = title,
            AccessType = access,
            Type = type,
            OrderNumber = number,
            ChatId = chatId
        };

    private static DbCharacter NewCharacter(Guid gameId, Guid authorId, string name, DateTimeOffset createdUtc) => new()
    {
        CharacterId = Guid.NewGuid(),
        GameId = gameId,
        AuthorId = authorId,
        Name = name,
        Status = CharacterStatus.Active,
        AccessPolicy = CharacterAccessPolicy.NoAccess,
        CreatedUtc = createdUtc
    };

    private static DbRoomAccess Grant(Guid roomId, Guid? characterId, Guid? readerId, RoomAccessPolicy policy) => new()
    {
        AccessId = Guid.NewGuid(),
        RoomId = roomId,
        CharacterId = characterId,
        ReaderUserId = readerId,
        Policy = policy
    };

    private static DbPost NewPost(Guid roomId, DbCharacter author, DateTimeOffset createdUtc, bool removed) => new()
    {
        PostId = Guid.NewGuid(),
        RoomId = roomId,
        CharacterId = author.CharacterId,
        AuthorId = author.AuthorId!.Value,
        CreatedUtc = createdUtc,
        GameText = "Пост",
        PrivateAddresseeSnapshotJson = "{}",
        IsRemoved = removed
    };

    private async Task Drop(World world)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        var games = new[] { world.GameId, world.OtherGameId };

        await db.Messages.IgnoreQueryFilters().Where(m => m.ChatId == world.ChatId).ExecuteDeleteAsync();
        await db.Chats.Where(c => c.ChatId == world.ChatId).ExecuteDeleteAsync();
        await db.Posts.IgnoreQueryFilters().Where(p => games.Contains(p.Room.GameId)).ExecuteDeleteAsync();
        await db.RoomAccesses.IgnoreQueryFilters().Where(a => games.Contains(a.Room.GameId)).ExecuteDeleteAsync();
        await db.Set<DbCharacter>().IgnoreQueryFilters().Where(c => games.Contains(c.GameId)).ExecuteDeleteAsync();
        await db.Rooms.IgnoreQueryFilters().Where(r => games.Contains(r.GameId)).ExecuteDeleteAsync();
        await db.Set<DbGame>().IgnoreQueryFilters().Where(g => games.Contains(g.GameId)).ExecuteDeleteAsync();
        await db.Users.IgnoreQueryFilters().Where(u => world.UserIds.Contains(u.UserId)).ExecuteDeleteAsync();
    }
}
