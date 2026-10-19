using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Unread;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Where "go to first unread" puts the reader down, and how much it says is left.
/// </summary>
/// <remarks>
/// Against the container Postgres: the reads are a union of per-room aggregates from each room's last-read moment.
/// The assertions state what the reader sees, independent of how the rooms are asked.
/// </remarks>
public class FirstUnreadRepositoryShould : IntegrationTestBase
{
    public FirstUnreadRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static readonly DateTime Base = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LandOnTheEarliestUnreadPostOfTheFirstRoomThatHasOne()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var game = await AddGameAsync(dbContext, rooms: 3);

        // Room 1 read to the end, room 2 read up to its first post, room 3 unread.
        var first = await AddPostsAsync(dbContext, game.Rooms[0], 2, minuteOffset: 0);
        var second = await AddPostsAsync(dbContext, game.Rooms[1], 3, minuteOffset: 10);
        await AddPostsAsync(dbContext, game.Rooms[2], 1, minuteOffset: 20);

        var lastRead = new Dictionary<Guid, DateTime>
        {
            [game.Rooms[0]] = first.Last().AddMinutes(1).UtcDateTime,
            [game.Rooms[1]] = second.First().UtcDateTime,
            [game.Rooms[2]] = DateTime.MinValue,
        };

        var result = await repository.FindFirstUnreadPost(game.Rooms, lastRead);

        result.Should().NotBeNull();
        result!.RoomId.Should().Be(game.Rooms[1], "room 1 is fully read and room 2 is the next in order");
        result.HasUnread.Should().BeTrue();
        // Second post of room 2: the number is the position within its own room.
        result.PostNumber.Should().Be(2);
        // Two left in room 2 and one in room 3 — the total spans every room, not
        // just the one the reader lands in.
        result.TotalUnreadCount.Should().Be(3);
    }

    [Fact]
    public async Task AnswerNothingWhenEveryRoomIsRead()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var game = await AddGameAsync(dbContext, rooms: 2);
        var first = await AddPostsAsync(dbContext, game.Rooms[0], 2, minuteOffset: 0);
        var second = await AddPostsAsync(dbContext, game.Rooms[1], 2, minuteOffset: 10);

        var lastRead = new Dictionary<Guid, DateTime>
        {
            [game.Rooms[0]] = first.Last().AddMinutes(1).UtcDateTime,
            [game.Rooms[1]] = second.Last().AddMinutes(1).UtcDateTime,
        };

        var result = await repository.FindFirstUnreadPost(game.Rooms, lastRead);

        result.Should().BeNull();
    }

    [Fact]
    public async Task StartAtTheFirstPostOfTheGameAndCountThemAll()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var game = await AddGameAsync(dbContext, rooms: 3);
        // The first room is empty: the entry point is the first room that holds
        // anything, not the first room.
        var second = await AddPostsAsync(dbContext, game.Rooms[1], 2, minuteOffset: 10);
        await AddPostsAsync(dbContext, game.Rooms[2], 3, minuteOffset: 20);

        var result = await repository.GetFirstPostInRooms(game.Rooms);

        result.Should().NotBeNull();
        result!.RoomId.Should().Be(game.Rooms[1]);
        result.PostNumber.Should().Be(1);
        result.TotalUnreadCount.Should().Be(5);
        result.HasUnread.Should().BeTrue();

        var earliest = FindPost(dbContext, game.Rooms[1], second.First());
        result.PostId.Should().Be(earliest);
    }

    [Fact]
    public async Task EndOnTheLastPostOfTheLastRoomThatHasOne()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var game = await AddGameAsync(dbContext, rooms: 3);
        await AddPostsAsync(dbContext, game.Rooms[0], 1, minuteOffset: 0);
        var second = await AddPostsAsync(dbContext, game.Rooms[1], 3, minuteOffset: 10);
        // The last room is empty, so the answer is the room before it.

        var result = await repository.GetLastPostInRooms(game.Rooms);

        result.Should().NotBeNull();
        result!.RoomId.Should().Be(game.Rooms[1]);
        // The number is the position in its own room, and the reader has nothing
        // unread — that is what puts them at the end rather than at a post.
        result.PostNumber.Should().Be(3);
        result.TotalUnreadCount.Should().Be(0);
        result.HasUnread.Should().BeFalse();

        var latest = FindPost(dbContext, game.Rooms[1], second.Last());
        result.PostId.Should().Be(latest);
    }

    [Fact]
    public async Task AnswerNothingForAGameWithoutRooms()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();

        var none = Array.Empty<Guid>();

        (await repository.FindFirstUnreadPost(none, new Dictionary<Guid, DateTime>())).Should().BeNull();
        (await repository.GetFirstPostInRooms(none)).Should().BeNull();
        (await repository.GetLastPostInRooms(none)).Should().BeNull();
    }

    /// <summary>
    /// The rooms are walked in the menu's order: by place, and of two sharing one, the room created first.
    /// </summary>
    [Fact]
    public async Task WalkTheRoomsInTheOrderTheMenuListsThem()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFirstUnreadRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var game = await AddGameAsync(dbContext, rooms: 0);
        // Written last first, one row at a time, so the order the rows lie in
        // is not the answer.
        var third = await AddRoomAsync(dbContext, game.GameId, number: 3, place: 1);
        var second = await AddRoomAsync(dbContext, game.GameId, number: 2, place: 1);
        var last = await AddRoomAsync(dbContext, game.GameId, number: 1, place: 2);

        (await repository.GetAccessibleRoomIds(game.GameId, game.UserId))
            .Should().Equal(second, third, last);
    }

    private static async Task<Guid> AddRoomAsync(DmDbContext dbContext, Guid gameId, int number, double place)
    {
        var roomId = Guid.NewGuid();
        dbContext.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = number,
            Title = "Room " + number,
            AccessType = RoomAccessType.Open,
            OrderNumber = place,
        });
        await dbContext.SaveChangesAsync();
        return roomId;
    }

    private sealed record GameRooms(Guid UserId, Guid GameId, IReadOnlyList<Guid> Rooms);

    private static async Task<GameRooms> AddGameAsync(DmDbContext dbContext, int rooms)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"unrd{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..5],
            Title = "Game for the first-unread check",
            MasterId = userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });

        var roomIds = new List<Guid>();
        for (var i = 0; i < rooms; i++)
        {
            var roomId = Guid.NewGuid();
            roomIds.Add(roomId);
            dbContext.Rooms.Add(new DbRoom
            {
                RoomId = roomId,
                GameId = gameId,
                RoomNumber = i + 1,
                Title = "Room " + (i + 1),
                AccessType = RoomAccessType.Open,
                OrderNumber = i + 1,
            });
        }

        await dbContext.SaveChangesAsync();
        return new GameRooms(userId, gameId, roomIds);
    }

    /// <summary>
    /// Posts a minute apart, so every moment in a game is distinct and the order
    /// under test is the one the data states rather than an insertion accident.
    /// </summary>
    private static async Task<IReadOnlyList<DateTimeOffset>> AddPostsAsync(
        DmDbContext dbContext, Guid roomId, int count, int minuteOffset)
    {
        var moments = new List<DateTimeOffset>();
        var game = dbContext.Rooms.Single(r => r.RoomId == roomId).GameId;
        var authorId = dbContext.Games.Single(g => g.GameId == game).MasterId;

        for (var i = 0; i < count; i++)
        {
            var createdUtc = new DateTimeOffset(Base.AddMinutes(minuteOffset + i));
            moments.Add(createdUtc);
            dbContext.Posts.Add(new DbPost
            {
                PostId = Guid.NewGuid(),
                RoomId = roomId,
                AuthorId = authorId,
                GameText = "text",
                CreatedUtc = createdUtc,
            });
        }

        await dbContext.SaveChangesAsync();
        return moments;
    }

    private static Guid FindPost(DmDbContext dbContext, Guid roomId, DateTimeOffset createdUtc) =>
        dbContext.Posts.Single(p => p.RoomId == roomId && p.CreatedUtc == createdUtc).PostId;
}
