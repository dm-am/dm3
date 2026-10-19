using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Helpers;

/// <summary>
/// The rows the post repository suites read back through the repository: a
/// master with a game and an open room of their own, and posts in it written by
/// characters, with or without reviews.
/// </summary>
/// <remarks>Written straight to the container Postgres, so the rows meet production's constraints.</remarks>
public abstract class PostRepositoryTestBase : IntegrationTestBase
{
    protected PostRepositoryTestBase(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    protected sealed record GameContext(Guid UserId, Guid GameId, Guid RoomId);

    /// <summary>A character in the game with one post in the room.</summary>
    protected static async Task<Guid> AddPostByNewCharacterAsync(
        DmDbContext dbContext, GameContext context)
    {
        var characterId = Guid.NewGuid();

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = context.GameId,
            AuthorId = context.UserId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.Posts.Add(new DbPost
        {
            PostId = Guid.NewGuid(),
            RoomId = context.RoomId,
            CharacterId = characterId,
            AuthorId = context.UserId,
            GameText = "text",
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return characterId;
    }

    /// <summary>
    /// A fresh master, game and room per test: the fixture's database is shared
    /// and seeded, so fixed identifiers would collide.
    /// </summary>
    protected static async Task<GameContext> AddGameWithRoomAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"post{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            PublicId = UniquePublicId(),
            Title = "Game for the rated-post ordering check",
            MasterId = userId,
            Status = ModuleStatus.Active,
            PremoderationStatus = PremoderationStatus.Approved,
        });
        dbContext.Rooms.Add(new DbRoom
        {
            RoomId = roomId,
            GameId = gameId,
            RoomNumber = 1,
            Title = "Room",
            AccessType = RoomAccessType.Open,
            OrderNumber = 1,
        });
        await dbContext.SaveChangesAsync();

        return new GameContext(userId, gameId, roomId);
    }

    /// <summary>
    /// A post authored by a character with a set of reviews; negative and removed ones keep the rating from being a count.
    /// </summary>
    protected static async Task<Guid> AddRatedPostAsync(
        DmDbContext dbContext, GameContext context, int positiveReviews,
        DateTimeOffset? reviewTime = null, DateTimeOffset? postCreatedUtc = null,
        int negativeReviews = 0, int removedReviews = 0, string gameText = "text")
    {
        var characterId = Guid.NewGuid();
        var postId = Guid.NewGuid();
        var baseTime = reviewTime ?? DateTimeOffset.UtcNow;

        dbContext.Characters.Add(new DbCharacter
        {
            CharacterId = characterId,
            GameId = context.GameId,
            AuthorId = context.UserId,
            Status = CharacterStatus.Active,
            Name = "Char " + Guid.NewGuid().ToString("N")[..6],
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        dbContext.Posts.Add(new DbPost
        {
            PostId = postId,
            RoomId = context.RoomId,
            CharacterId = characterId,
            AuthorId = context.UserId,
            GameText = gameText,
            CreatedUtc = postCreatedUtc ?? DateTimeOffset.UtcNow,
        });

        // One reviewer per review: PostReviews is uniquely indexed on (AuthorId, PostId).
        var written = 0;
        void AddReview(short sign, bool isRemoved)
        {
            var reviewerId = Guid.NewGuid();
            dbContext.Users.Add(new DbUser
            {
                UserId = reviewerId,
                Username = $"rev{reviewerId:N}"[..20],
                Email = $"{reviewerId:N}@example.com",
                PasswordHash = "hash",
                Salt = "salt",
            });
            dbContext.PostReviews.Add(new DbPostReview
            {
                PostReviewId = Guid.NewGuid(),
                PostId = postId,
                AuthorId = reviewerId,
                PostAuthorId = context.UserId,
                GameId = context.GameId,
                SignValue = sign,
                IsRemoved = isRemoved,
                CreatedUtc = baseTime.AddSeconds(written++),
            });
        }

        for (var i = 0; i < positiveReviews; i++) AddReview(1, isRemoved: false);
        for (var i = 0; i < negativeReviews; i++) AddReview(-1, isRemoved: false);
        // A removed review is in neither number, and it is the only way to tell
        // a filtered sum from an unfiltered one.
        for (var i = 0; i < removedReviews; i++) AddReview(1, isRemoved: true);

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// A public id no other row holds: the column is NOT NULL and uniquely indexed.
    /// </summary>
    private static string UniquePublicId() => Guid.NewGuid().ToString("N")[..10];
}
