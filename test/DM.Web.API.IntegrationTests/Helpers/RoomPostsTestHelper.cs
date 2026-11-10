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

// A fresh game per test: a shared room holds a number of posts nobody here can state.
public static class RoomPostsTestHelper
{
    public sealed record SeededRoom(
        Guid GameId,
        Guid RoomId,
        GeneralUser Master,
        GeneralUser Player,
        Guid MasterCharacterId,
        Guid PlayerCharacterId);

    public const string GameTitle = "Game for the room reads";

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
            Title = GameTitle,
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

    // Reviews of one post, a new reviewer each: a review is one person's, and the table refuses a
    // second from the same author.
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
