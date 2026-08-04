using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.PostReviews;
using DM.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbCharacter = DM.Infrastructure.Persistence.Entities.Game.Characters.Character;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbLike = DM.Infrastructure.Persistence.Entities.Shared.Like;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;
using DbRoom = DM.Infrastructure.Persistence.Entities.Game.Posts.Room;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// Post reviews are the one entity of the review system that carries likes, and
/// the collection came back empty from every read path.
/// </summary>
/// <remarks>
/// Likes are polymorphic (EntityType + EntityId), so nothing on the review row
/// points at them: the AutoMapper profile ignores the member and a second pass
/// has to fill it. Without that pass the domain model, the API contract and the
/// seed all had likes while the site had none — a failure a mapping test cannot
/// see, because the mapping is correct.
/// </remarks>
public class PostReviewRepositoryShould : IntegrationTestBase
{
    public PostReviewRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ReadTheLikersOfEveryReviewOfAPost()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPostReviewRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var context = await AddGameWithRoomAsync(dbContext);
        var postId = await AddPostAsync(dbContext, context);
        var reviewId = await AddReviewAsync(dbContext, context, postId);

        var firstLiker = await AddUserAsync(dbContext);
        var secondLiker = await AddUserAsync(dbContext);
        var formerLiker = await AddUserAsync(dbContext);
        AddLike(dbContext, reviewId, firstLiker, LikeEntityType.PostReview, isRemoved: false);
        AddLike(dbContext, reviewId, secondLiker, LikeEntityType.PostReview, isRemoved: false);
        // A withdrawn like, and a like of another entity type carrying the same
        // id: the fill has to key on both columns, the way the table is keyed.
        AddLike(dbContext, reviewId, formerLiker, LikeEntityType.PostReview, isRemoved: true);
        AddLike(dbContext, reviewId, formerLiker, LikeEntityType.Topic, isRemoved: false);
        await dbContext.SaveChangesAsync();

        var reviews = (await repository.GetAsync(
            postId,
            new PagingData(new PagingQuery { Skip = 0, Take = 10 }, 10, 1))).ToList();

        reviews.Should().ContainSingle();
        reviews[0].Likes.Select(u => u.UserId).Should().BeEquivalentTo(
            new[] { firstLiker, secondLiker },
            "a withdrawn like is not a like, and a like of another entity type is not this review's");

        // The single-review path feeds the same badge, so it fills the same way.
        var single = await repository.GetAsync(reviewId);
        single.Should().NotBeNull();
        single!.Likes.Should().HaveCount(2);
    }

    private sealed record GameContext(Guid UserId, Guid GameId, Guid RoomId);

    /// <summary>
    /// A fresh master, game and room per test: the fixture's database is shared
    /// and seeded, so fixed identifiers would collide.
    /// </summary>
    private static async Task<GameContext> AddGameWithRoomAsync(DmDbContext dbContext)
    {
        var userId = await AddUserAsync(dbContext);
        var gameId = Guid.NewGuid();
        var roomId = Guid.NewGuid();

        dbContext.Games.Add(new DbGame
        {
            GameId = gameId,
            // PublicId is NOT NULL and uniquely indexed.
            PublicId = Guid.NewGuid().ToString("N")[..10],
            Title = "Game for the review-likes check",
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

    private static async Task<Guid> AddPostAsync(DmDbContext dbContext, GameContext context)
    {
        var characterId = Guid.NewGuid();
        var postId = Guid.NewGuid();

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

        await dbContext.SaveChangesAsync();
        return postId;
    }

    /// <summary>
    /// One positive review by somebody other than the post's author — the pair
    /// (AuthorId, PostId) is uniquely indexed.
    /// </summary>
    private static async Task<Guid> AddReviewAsync(
        DmDbContext dbContext, GameContext context, Guid postId)
    {
        var reviewId = Guid.NewGuid();
        var reviewerId = await AddUserAsync(dbContext);

        dbContext.PostReviews.Add(new DbPostReview
        {
            PostReviewId = reviewId,
            PostId = postId,
            AuthorId = reviewerId,
            PostAuthorId = context.UserId,
            GameId = context.GameId,
            SignValue = 1,
            Text = "Отличный отыгрыш!",
            CreatedUtc = DateTimeOffset.UtcNow,
        });

        await dbContext.SaveChangesAsync();
        return reviewId;
    }

    private static async Task<Guid> AddUserAsync(DmDbContext dbContext)
    {
        var userId = Guid.NewGuid();

        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id is
            // truncated rather than used whole.
            Username = $"rev{userId:N}"[..20],
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });

        await dbContext.SaveChangesAsync();
        return userId;
    }

    private static void AddLike(
        DmDbContext dbContext, Guid entityId, Guid userId, LikeEntityType entityType, bool isRemoved)
    {
        dbContext.Likes.Add(new DbLike
        {
            LikeId = Guid.NewGuid(),
            EntityId = entityId,
            EntityType = entityType,
            UserId = userId,
            IsRemoved = isRemoved,
        });
    }
}
