using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Posts;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Runs against the container Postgres. The ordering under test is produced by
/// a GroupBy the InMemory provider cannot translate, so the version of this
/// test that ran there had to stub the hydration step out — which is to say the
/// production query was never executed by anything. Here it is, joins included.
/// </summary>
public class PostRepositoryShould : IntegrationTestBase
{
    public PostRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task DefaultSortRatedPostsByRatingDescending()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var lowRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 1);
        var highRated = await AddRatedPostAsync(dbContext, context, positiveReviews: 3);

        // No SortBy: the repository default must be rating desc (doc 4.2.3.5.6).
        // Scoped to this test's own game — the fixture database is seeded and
        // shared, so an unscoped query would rank against everyone else's posts.
        var (posts, total) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            GameId = context.GameId,
        });

        var ordered = posts.ToList();
        total.Should().Be(2);
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(highRated);
        ordered[0].Rating.Should().Be(3);
        ordered[1].Id.Should().Be(lowRated);
        ordered[1].Rating.Should().Be(1);
    }

    [Fact]
    public async Task SortByLastReviewOrdersByMostRecentReview()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        // The higher-rated post was reviewed earlier; the lower-rated one got a
        // more recent review. lastreview must invert the rating-desc order.
        var highRatedOlderReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 3, reviewTime: DateTimeOffset.UtcNow.AddHours(-1));
        var lowRatedNewerReview = await AddRatedPostAsync(
            dbContext, context, positiveReviews: 1, reviewTime: DateTimeOffset.UtcNow);

        var (posts, _) = await repository.GetRated(new PostsQuery
        {
            Take = 10,
            SortBy = "lastreview",
            GameId = context.GameId,
        });

        var ordered = posts.ToList();
        ordered.Should().HaveCount(2);
        ordered[0].Id.Should().Be(lowRatedNewerReview);
        ordered[1].Id.Should().Be(highRatedOlderReview);
    }

    private sealed record GameContext(Guid UserId, Guid GameId, Guid RoomId);

    /// <summary>
    /// A fresh master, game and room per test: the fixture's database is shared
    /// and seeded, so fixed identifiers would collide.
    /// </summary>
    private static async Task<GameContext> AddGameWithRoomAsync(DmDbContext dbContext)
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
    /// A post authored by a character, with a set of positive reviews. The
    /// post's rating is the sum of the review sign values.
    /// </summary>
    private static async Task<Guid> AddRatedPostAsync(
        DmDbContext dbContext, GameContext context, int positiveReviews, DateTimeOffset? reviewTime = null)
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
            GameText = "text",
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        // One reviewer per review. PostReviews is uniquely indexed on
        // (AuthorId, PostId) — a rating is a count of distinct people, not of
        // one person's clicks. The InMemory provider had no such index, so the
        // version of this test that ran there built every rating out of rows the
        // database would have refused.
        for (var i = 0; i < positiveReviews; i++)
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
                SignValue = 1,
                CreatedUtc = baseTime.AddSeconds(i),
            });
        }

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// A public id no other row holds. The column is NOT NULL and uniquely
    /// indexed — a constraint the InMemory provider did not have, which is why
    /// the version of this test that ran there could omit the value entirely.
    /// </summary>
    private static string UniquePublicId() => Guid.NewGuid().ToString("N")[..10];
}
