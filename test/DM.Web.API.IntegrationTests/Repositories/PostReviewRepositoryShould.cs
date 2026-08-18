using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostReviews;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <summary>
/// The review row and the post author's quality rating, written as one thing.
/// </summary>
/// <remarks>
/// QualityRating is a stored column and not a sum over the reviews: nothing
/// recomputes it, and the post's own rating - which is summed - stays right no
/// matter what happens to it, so a drift here has nothing to be caught by. The
/// counter used to be moved by a call of its own that committed before the row
/// it belonged to was written, twice over on a sign change, and a refusal in
/// either gap left a profile carrying a sign the review does not have.
///
/// Against the container Postgres, because the thing under test is the rollback:
/// the InMemory provider has no transactions to roll back and no ExecuteUpdate to
/// run, so a test there would assert the double rather than the write. The
/// failure is a foreign key on the modifier - a user identifier nobody holds -
/// which the row write is refused for after the counter has already moved inside
/// the transaction.
/// </remarks>
public class PostReviewRepositoryShould : IntegrationTestBase
{
    public PostReviewRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The post every review here lands on, and the user who wrote it.</summary>
    private static Guid PostId => TestConstants.TestGamePostId;

    private static Guid PostAuthorId => TestConstants.TestUserId;

    /// <summary>Whoever writes the reviews below; not the post's author.</summary>
    private static Guid RaterId => TestConstants.MentorUserId;

    private async Task<Guid> SeedReview(short sign)
    {
        var reviewId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.PostReviews.Add(new DbPostReview
        {
            PostReviewId = reviewId,
            AuthorId = RaterId,
            PostId = PostId,
            PostAuthorId = PostAuthorId,
            GameId = TestConstants.TestGameId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = "Исходный текст оценки",
            SignValue = sign,
            IsRemoved = false
        });
        await db.SaveChangesAsync();
        return reviewId;
    }

    /// <summary>
    /// IgnoreQueryFilters: a row a test here has had removed is hidden by the
    /// global soft-delete filter, and the cleanup would leave it behind.
    /// </summary>
    private async Task RemoveReview(Guid reviewId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.PostReviews.IgnoreQueryFilters()
            .Where(r => r.PostReviewId == reviewId).ExecuteDeleteAsync();
    }

    private async Task<DbPostReview?> StoredReview(Guid reviewId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.PostReviews.IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.PostReviewId == reviewId);
    }

    private async Task<int> QualityRatingOf(Guid userId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Users.Where(u => u.UserId == userId).Select(u => u.QualityRating).FirstAsync();
    }

    /// <summary>The counter is shared with the rest of the fixture, so put it back.</summary>
    private async Task RestoreQualityRating(Guid userId, int value)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        user.QualityRating = value;
        await db.SaveChangesAsync();
    }

    private IPostReviewRepository Repository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IPostReviewRepository>();

    /// <summary>
    /// The edit that cannot be written does not pay the counter either.
    /// </summary>
    [Fact]
    public async Task LeaveTheCounterWhereItWasWhenTheRowWriteIsRefused()
    {
        var reviewId = await SeedReview(sign: 1);
        try
        {
            var before = await QualityRatingOf(PostAuthorId);
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            // A modifier nobody is: the foreign key refuses the row, and the
            // counter this same transaction has already moved goes back with it.
            var act = async () => await Repository(scope).UpdateAsync(
                new UpdatePostReviewEntity(
                    reviewId,
                    Sign: ReviewSign.Negative,
                    ModifiedUtc: DateTimeOffset.UtcNow,
                    ModifiedByUserId: Guid.NewGuid()),
                qualityRatingDelta: -2);

            await act.Should().ThrowAsync<DbUpdateException>();

            (await QualityRatingOf(PostAuthorId)).Should().Be(before,
                "the counter is stored and nothing recomputes it, so a delta that " +
                "outlives the write it belongs to is a drift with nothing to catch it");
            (await StoredReview(reviewId))!.SignValue.Should().Be(1, "and the sign it was paid for is unchanged");
        }
        finally
        {
            await RemoveReview(reviewId);
        }
    }

    /// <summary>
    /// The other half of the same rule: when the write goes through, both land.
    /// </summary>
    [Fact]
    public async Task MoveTheCounterWithTheSignItPaysFor()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await QualityRatingOf(PostAuthorId);
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            await Repository(scope).UpdateAsync(
                new UpdatePostReviewEntity(
                    reviewId,
                    Sign: ReviewSign.Negative,
                    ModifiedUtc: DateTimeOffset.UtcNow,
                    ModifiedByUserId: RaterId),
                qualityRatingDelta: -2);

            (await StoredReview(reviewId))!.SignValue.Should().Be(-1);
            (await QualityRatingOf(PostAuthorId)).Should().Be(before - 2,
                "the plus taken back and the minus applied, in the write that moved the sign");
        }
        finally
        {
            await RemoveReview(reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// The removal is the same method with the sign given back, and it rolls back
    /// the same way.
    /// </summary>
    [Fact]
    public async Task GiveTheSignBackWithTheRemovalAndNotBeforeIt()
    {
        var reviewId = await SeedReview(sign: -1);
        var before = await QualityRatingOf(PostAuthorId);
        try
        {
            // A scope apiece, as two requests would have: a context whose
            // SaveChanges was refused still carries the change it could not write.
            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                var refused = async () => await Repository(scope).UpdateAsync(
                    new UpdatePostReviewEntity(
                        reviewId,
                        IsRemoved: true,
                        DeletedUtc: DateTimeOffset.UtcNow,
                        DeletedByUserId: Guid.NewGuid()),
                    qualityRatingDelta: 1);

                await refused.Should().ThrowAsync<DbUpdateException>();
            }

            (await QualityRatingOf(PostAuthorId)).Should().Be(before);
            (await StoredReview(reviewId))!.IsRemoved.Should().BeFalse("the removal was refused, not half-applied");

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                await Repository(scope).UpdateAsync(
                    new UpdatePostReviewEntity(
                        reviewId,
                        IsRemoved: true,
                        DeletedUtc: DateTimeOffset.UtcNow,
                        DeletedByUserId: RaterId),
                    qualityRatingDelta: 1);
            }

            (await StoredReview(reviewId))!.IsRemoved.Should().BeTrue();
            (await QualityRatingOf(PostAuthorId)).Should().Be(before + 1);
        }
        finally
        {
            await RemoveReview(reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// And the way in: the review the unique index refuses costs the post author
    /// nothing.
    /// </summary>
    /// <remarks>
    /// One active review per author-post pair, and the service checks for one
    /// before writing - this is the race where two requests both pass that check.
    /// The counter moved for the loser of that race is the drift the check cannot
    /// prevent.
    /// </remarks>
    [Fact]
    public async Task PayNothingForAReviewTheUniqueIndexRefuses()
    {
        var reviewId = await SeedReview(sign: 1);
        try
        {
            var before = await QualityRatingOf(PostAuthorId);
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            var act = async () => await Repository(scope).CreateAsync(
                new CreatePostReviewEntity
                {
                    PostReviewId = Guid.NewGuid(),
                    AuthorId = RaterId,
                    PostId = PostId,
                    PostAuthorId = PostAuthorId,
                    GameId = TestConstants.TestGameId,
                    CreatedUtc = DateTimeOffset.UtcNow,
                    Sign = ReviewSign.Positive,
                    Text = "Вторая оценка того же поста"
                },
                qualityRatingDelta: 1);

            await act.Should().ThrowAsync<DuplicateEntityException>();

            (await QualityRatingOf(PostAuthorId)).Should().Be(before);
        }
        finally
        {
            await RemoveReview(reviewId);
        }
    }
}
