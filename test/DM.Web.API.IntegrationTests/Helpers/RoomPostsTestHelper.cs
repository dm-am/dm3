using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUnreadCounter = DM.Infrastructure.Persistence.Entities.Shared.UnreadCounter;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// A game with one room of its own, and the posts, reviews and read marks the
/// room suites put into it.
/// </summary>
/// <remarks>
/// A fresh game per test rather than the seeded room: the fixture's database is
/// shared, and a room other suites write into holds a number of posts nobody here
/// can state.
/// </remarks>
public static class RoomPostsTestHelper
{
    /// <summary>
    /// The game and room, with the master and one player, a character each.
    /// </summary>
    public sealed record SeededRoom(
        Guid GameId,
        Guid RoomId,
        GeneralUser Master,
        GeneralUser Player,
        Guid MasterCharacterId,
        Guid PlayerCharacterId);

    /// <summary>An approved, active game with one room and two characters in it.</summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="access">Who the room is open to</param>
    public static async Task<SeededRoom> AddGameWithRoomAsync(
        DatabaseFixture dbFixture, RoomAccessType access = RoomAccessType.Open)
    {
        await using var db = dbFixture.CreateDbContext();

        var master = AddUser(db, "mst");
        var player = AddUser(db, "plr");
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        db.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the room reads",
            MasterId = master.UserId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        db.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Room",
            AccessType = access,
            OrderNumber = 1,
        });

        var masterCharacterId = AddCharacter(db, gameId, master.UserId);
        var playerCharacterId = AddCharacter(db, gameId, player.UserId);

        await db.SaveChangesAsync();
        return new SeededRoom(gameId, roomId, master, player, masterCharacterId, playerCharacterId);
    }

    /// <summary>One post, written straight to the table.</summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="roomId">Room the post is in</param>
    /// <param name="authorId">Who wrote it</param>
    /// <param name="characterId">Character it is written by, or null</param>
    /// <param name="createdUtc">Moment it was written</param>
    /// <param name="text">In-game text</param>
    /// <param name="isRemoved">Whether it has been deleted</param>
    public static async Task<Guid> AddPostAsync(
        DatabaseFixture dbFixture,
        Guid roomId,
        Guid authorId,
        Guid? characterId,
        DateTimeOffset createdUtc,
        string text = "text",
        bool isRemoved = false)
    {
        await using var db = dbFixture.CreateDbContext();
        var postId = Guid.NewGuid();

        db.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = roomId,
            AuthorId = authorId,
            CharacterId = characterId,
            CreatedUtc = createdUtc,
            GameText = text,
            IsRemoved = isRemoved,
        });

        await db.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// Reviews of one post, a new reviewer each: a review is one person's, and
    /// the table refuses a second from the same author.
    /// </summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="room">Room the post is in</param>
    /// <param name="postId">Post under review</param>
    /// <param name="postAuthorId">Author of the post</param>
    /// <param name="reviews">Sign and moment of each review</param>
    public static async Task AddReviewsAsync(
        DatabaseFixture dbFixture,
        SeededRoom room,
        Guid postId,
        Guid postAuthorId,
        params (short Sign, DateTimeOffset At)[] reviews)
    {
        await using var db = dbFixture.CreateDbContext();

        foreach (var (sign, at) in reviews)
        {
            var reviewer = AddUser(db, "rev");
            db.PostReviews.Add(new DbPostReview
            {
                PostReviewId = Guid.NewGuid(),
                PostId = postId,
                AuthorId = reviewer.UserId,
                PostAuthorId = postAuthorId,
                GameId = room.GameId,
                SignValue = sign,
                CreatedUtc = at,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The reader's mark on the room: everything written after
    /// <paramref name="lastReadUtc"/> is unread for them.
    /// </summary>
    /// <param name="dbFixture">Database fixture for direct DB access</param>
    /// <param name="room">Room being read</param>
    /// <param name="userId">Reader</param>
    /// <param name="lastReadUtc">Moment the room was last read</param>
    public static async Task MarkReadAsync(
        DatabaseFixture dbFixture, SeededRoom room, Guid userId, DateTime lastReadUtc)
    {
        await using var db = dbFixture.CreateDbContext();

        db.Set<DbUnreadCounter>().Add(new DbUnreadCounter
        {
            UserId = userId,
            EntityId = room.RoomId,
            EntryType = UnreadEntryType.Message,
            ParentId = room.GameId,
            LastReadUtc = DateTime.SpecifyKind(lastReadUtc, DateTimeKind.Utc),
            Counter = 0,
        });

        await db.SaveChangesAsync();
    }

    private static GeneralUser AddUser(Infrastructure.Persistence.DmDbContext db, string prefix)
    {
        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed, so the id is truncated
        // rather than used whole.
        var username = $"{prefix}{userId:N}"[..20];

        db.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });

        return new GeneralUser { UserId = userId, Username = username, Role = UserRole.RegularUser };
    }

    private static Guid AddCharacter(Infrastructure.Persistence.DmDbContext db, Guid gameId, Guid authorId)
    {
        var characterId = Guid.NewGuid();

        db.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = gameId,
            AuthorId = authorId,
            Status = CharacterStatus.Active,
            Name = "Char " + characterId.ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        return characterId;
    }
}
