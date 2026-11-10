using System.Net;
using System.Text;
using System.Text.Json;
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

// Through HTTP: the order is written by one request and read by another, through the service and
// the mapper.
public class RoomListShould : IntegrationTestBase
{
    public RoomListShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

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

    // Moves one after another: each starts from the order the one before it left, which is what the
    // settings send for one "Сохранить".
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
    /// "[Лес] - Густав, Эодор": a guest and a stranger see the closed room with the characters let in to write,
    /// in joining order; a dead one named as the old site does, a removed one as nobody. The master reads its
    /// accesses instead.
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

    private async Task<World> Seed()
    {
        var now = DateTimeOffset.UtcNow;
        var gameId = Guid.NewGuid();
        var suffix = gameId.ToString("N")[..6];

        var master = NewUser("rlm");
        var stranger = NewUser("rls");
        var players = Enumerable.Range(0, 5).Select(_ => NewUser("rlp")).ToArray();
        var reader = NewUser("rlr");
        var world = new World(
            GameId: gameId,
            OtherGameId: Guid.NewGuid(),
            Hall: Guid.NewGuid(),
            Closed: Guid.NewGuid(),
            Chat: Guid.NewGuid(),
            Foreign: Guid.NewGuid(),
            ChatId: Guid.NewGuid(),
            Master: Caller(master),
            Stranger: Caller(stranger),
            UserIds: [master.UserId, stranger.UserId, reader.UserId, .. players.Select(p => p.UserId)],
            FirstWriter: $"Густав {suffix}",
            SecondWriter: $"Эодор {suffix}",
            DeadWriter: $"Николай {suffix}");

        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DmDbContext>();
        db.Users.AddRange(master, stranger, reader);
        db.Users.AddRange(players);
        db.Set<DbGame>().AddRange(NewGame(world.GameId, master.UserId, now), NewGame(world.OtherGameId, master.UserId, now));

        AddRooms(db, world);
        var first = AddWriters(db, world, players, reader, suffix, now);
        AddMessages(db, world, first, players[0], now);

        await db.SaveChangesAsync();

        return world;
    }

    private static void AddRooms(DmDbContext db, World world)
    {
        db.Set<DbRoom>().AddRange(
            NewRoom(world.Hall, world.GameId, 1, "Зала", RoomAccessType.Open, RoomType.Default, null),
            NewRoom(world.Closed, world.GameId, 2, "Лес", RoomAccessType.Private, RoomType.Default, null),
            NewRoom(world.Chat, world.GameId, 3, "Шепот", RoomAccessType.Open, RoomType.Chat, world.ChatId),
            NewRoom(world.Foreign, world.OtherGameId, 1, "Чужая", RoomAccessType.Open, RoomType.Default, null));
        db.Chats.Add(new DbChat
        {
            ChatId = world.ChatId,
            Type = ChatType.GameRoom,
            Title = "Шепот",
            RoomId = world.Chat,
            PublicId = $"l{world.ChatId:N}"[..10]
        });
    }

    private static DbCharacter AddWriters(
        DmDbContext db, World world, DbUser[] players, DbUser reader, string suffix, DateTimeOffset now)
    {
        // Joined in the order the names are expected in, the second writer
        // added to the room first: the names follow the game, not the grants.
        var second = NewCharacter(world.GameId, players[1].UserId, world.SecondWriter, now.AddDays(-2));
        var first = NewCharacter(world.GameId, players[0].UserId, world.FirstWriter, now.AddDays(-3));
        var onlyReads = NewCharacter(world.GameId, players[2].UserId, $"Читающий {suffix}", now.AddDays(-4));
        var dead = NewCharacter(world.GameId, players[3].UserId, world.DeadWriter, now.AddDays(-1));
        dead.Status = CharacterStatus.Retired;
        dead.IsDead = true;
        // Joined first: named at all, he would head the line.
        var removed = NewCharacter(world.GameId, players[4].UserId, $"Удаленный {suffix}", now.AddDays(-5));
        removed.IsRemoved = true;
        db.Set<DbCharacter>().AddRange(second, first, onlyReads, dead, removed);
        db.Set<DbRoomAccess>().AddRange(
            Grant(world.Closed, removed.CharacterId, null, RoomAccessPolicy.Full),
            Grant(world.Closed, dead.CharacterId, null, RoomAccessPolicy.Full),
            Grant(world.Closed, second.CharacterId, null, RoomAccessPolicy.Full),
            Grant(world.Closed, first.CharacterId, null, RoomAccessPolicy.Full),
            Grant(world.Closed, onlyReads.CharacterId, null, RoomAccessPolicy.ReadOnly),
            Grant(world.Closed, null, reader.UserId, RoomAccessPolicy.ReadOnly));
        return first;
    }

    private static void AddMessages(DmDbContext db, World world, DbCharacter first, DbUser player, DateTimeOffset now)
    {
        db.Set<DbPost>().AddRange(
            NewPost(world.Hall, first, now.AddHours(-3), removed: false),
            NewPost(world.Hall, first, now.AddHours(-2), removed: false),
            NewPost(world.Hall, first, now.AddHours(-1), removed: true),
            NewPost(world.Closed, first, now.AddHours(-1), removed: false));
        db.Messages.AddRange(Enumerable.Range(0, 4).Select(i => new DbMessage
        {
            MessageId = Guid.NewGuid(),
            ChatId = world.ChatId,
            UserId = player.UserId,
            CreatedUtc = now.AddMinutes(-10 + i),
            Text = $"Сообщение {i}",
            IsRemoved = i == 3
        }));
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
