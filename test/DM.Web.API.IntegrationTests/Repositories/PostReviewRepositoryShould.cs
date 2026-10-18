using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostReviews;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
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
            var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            // A modifier nobody is: the foreign key refuses the row, and the
            // counter this same transaction has already moved goes back with it.
            var act = async () => await Repository(scope).UpdateAsync(
                new UpdatePostReviewEntity(
                    reviewId,
                    Sign: ReviewSign.Negative,
                    ModifiedUtc: DateTimeOffset.UtcNow,
                    ModifiedByUserId: Guid.NewGuid()));

            await act.Should().ThrowAsync<DbUpdateException>();

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before,
                "the counter is stored and nothing recomputes it, so a delta that " +
                "outlives the write it belongs to is a drift with nothing to catch it");
            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.SignValue.Should().Be(1, "and the sign it was paid for is unchanged");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    /// <summary>
    /// The other half of the same rule: when the write goes through, both land.
    /// </summary>
    [Fact]
    public async Task MoveTheCounterWithTheSignItPaysFor()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            await Repository(scope).UpdateAsync(
                new UpdatePostReviewEntity(
                    reviewId,
                    Sign: ReviewSign.Negative,
                    ModifiedUtc: DateTimeOffset.UtcNow,
                    ModifiedByUserId: RaterId));

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.SignValue.Should().Be(-1);
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 2,
                "the plus taken back and the minus applied, in the write that moved the sign");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
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
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
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
                        DeletedByUserId: Guid.NewGuid()));

                await refused.Should().ThrowAsync<DbUpdateException>();
            }

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before);
            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeFalse("the removal was refused, not half-applied");

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                await Repository(scope).UpdateAsync(
                    new UpdatePostReviewEntity(
                        reviewId,
                        IsRemoved: true,
                        DeletedUtc: DateTimeOffset.UtcNow,
                        DeletedByUserId: RaterId));
            }

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before + 1);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
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
            var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
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

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    /// <summary>
    /// One review taken down twice at once gives its sign back once.
    /// </summary>
    /// <remarks>
    /// The author of the review and a senior moderator can both remove it, and
    /// the author can press it twice on a slow answer. What the counter owes used
    /// to be computed by the caller from the review it had read, so both removals
    /// carried the same "give the plus back" and the post author lost two points
    /// for one review — a stored column, and the row that would say the right
    /// number is the one that was removed.
    ///
    /// A scope apiece, as two requests would have, and a timeout over the pair:
    /// the second removal waits on the row lock, and a wait that never ends has
    /// to fail rather than hang the suite.
    /// </remarks>
    [Fact]
    public async Task GiveTheSignBackOnceWhenTwoRemovalsRaceForOneReview()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            await Task.WhenAll(Remove(reviewId), Remove(reviewId))
                .WaitAsync(TimeSpan.FromSeconds(30));

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 1,
                "the plus is given back by the removal that happened, and the one that " +
                "found the review already gone owes nothing");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// An edit that arrives after the removal moves nothing.
    /// </summary>
    /// <remarks>
    /// The author edits the sign in one tab while a senior moderator takes the
    /// review down in another. A removed review is worth nothing to the post
    /// author either way, so the difference the edit is worth is zero — which is
    /// what a write that reads the row itself can see and a caller holding the
    /// review as it was cannot.
    /// </remarks>
    [Fact]
    public async Task MoveNothingForAnEditQueuedBehindTheRemoval()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            await Remove(reviewId);

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 1);

            using (var scope = DatabaseFixture.Factory.Services.CreateScope())
            {
                await Repository(scope).UpdateAsync(
                    new UpdatePostReviewEntity(
                        reviewId,
                        Sign: ReviewSign.Negative,
                        ModifiedUtc: DateTimeOffset.UtcNow,
                        ModifiedByUserId: RaterId));
            }

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 1,
                "the review is removed and contributes nothing whatever its sign says, " +
                "so the edit has no difference to pay");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// A sign the request does not carry is the sign the row already holds.
    /// </summary>
    /// <remarks>
    /// Editing the text of a review is a request with no sign in it, and what
    /// the counter owes is the difference between the contribution the row will
    /// have and the one it has. Reading the missing sign as nothing would make
    /// that difference the whole sign taken back: the post author would pay for
    /// an edit that never touched the sign, and the row would go on holding a
    /// plus the counter no longer counts.
    /// </remarks>
    [Fact]
    public async Task KeepTheStoredSignWhenTheRequestCarriesOnlyText()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            await Repository(scope).UpdateAsync(
                new UpdatePostReviewEntity(
                    reviewId,
                    Text: "The review text, edited and nothing else",
                    ModifiedUtc: DateTimeOffset.UtcNow,
                    ModifiedByUserId: RaterId));

            var stored = (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!;
            stored.Text.Should().Be("The review text, edited and nothing else");
            stored.SignValue.Should().Be(1,
                "the request named no sign, so the row keeps the one it had");
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before,
                "and an edit that leaves the sign where it was owes the post author nothing");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// Taking down a review that carried no sign costs the post author nothing.
    /// </summary>
    /// <remarks>
    /// A neutral review is a comment with no weight, so its whole life is worth
    /// zero to the counter: nothing on the way in, nothing on the way out. It is
    /// the one sign that says whether the contribution is read off the row or
    /// guessed from the fact that a review exists.
    /// </remarks>
    [Fact]
    public async Task GiveNothingBackForTheRemovalOfANeutralReview()
    {
        var reviewId = await SeedReview(sign: 0);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            await Remove(reviewId);

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before,
                "the review was worth nothing while it stood, so there is nothing to give back");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// The audit names the removal that happened, not the one that arrived after.
    /// </summary>
    /// <remarks>
    /// The author of a review and a senior moderator can both take it down, and
    /// the second press finds the row already removed. The counter already owes
    /// nothing in that case; the audit columns have to hold still for the same
    /// reason, or the removal a moderator answers for is recorded under whoever
    /// pressed the button last.
    /// </remarks>
    [Fact]
    public async Task KeepTheFirstRemoversHandWhenTheReviewIsRemovedAgain()
    {
        var reviewId = await SeedReview(sign: 1);
        var before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
        try
        {
            await Remove(reviewId);
            var first = (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!;
            first.DeletedByUserId.Should().Be(RaterId);
            var firstUtc = first.DeletedUtc;

            await Remove(reviewId, TestConstants.SeniorModeratorUserId);

            var second = (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!;
            second.DeletedByUserId.Should().Be(RaterId,
                "the review was taken down once, and the hand recorded is the one that took it down");
            second.DeletedUtc.Should().Be(firstUtc, "along with the moment it happened");
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 1);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQualityRating(PostAuthorId, before);
        }
    }

    /// <summary>
    /// Reviews of one post written in the same moment come back in the same
    /// order on every read: the list numbers them "#1", "#2" by it.
    /// </summary>
    /// <remarks>
    /// Written in an order that is neither the order of their identifiers nor
    /// its reverse, so neither the order the rows were stored in nor an index
    /// read backwards can pass for the tie being closed. The moment is shared,
    /// as the seed shares it between two reviews of its showcase post, which is
    /// where the numbers were seen to swap between two loads of a room.
    /// </remarks>
    [Fact]
    public async Task CloseATieOfTheSameMomentByTheIdentifier()
    {
        var moment = DateTimeOffset.UtcNow.AddMinutes(-5);
        var low = Guid.Parse("7e57a11e-0000-0000-0000-000000000001");
        var middle = Guid.Parse("7e57a11e-0000-0000-0000-000000000002");
        var high = Guid.Parse("7e57a11e-0000-0000-0000-000000000003");
        var written = new[]
        {
            (Id: middle, AuthorId: TestConstants.TestimonialUserIds[10]),
            (Id: low, AuthorId: TestConstants.TestimonialUserIds[11]),
            (Id: high, AuthorId: TestConstants.TestimonialUserIds[12]),
        };
        try
        {
            foreach (var (id, authorId) in written)
            {
                await using var db = DatabaseFixture.CreateDbContext();
                db.PostReviews.Add(new DbPostReview
                {
                    PostReviewId = id,
                    AuthorId = authorId,
                    PostId = PostId,
                    PostAuthorId = PostAuthorId,
                    GameId = TestConstants.TestGameId,
                    CreatedUtc = moment,
                    Text = "Оценка в ту же минуту",
                    SignValue = 1,
                    IsRemoved = false
                });
                await db.SaveChangesAsync();
            }

            var ours = written.Select(w => w.Id).ToHashSet();
            var paging = new PagingData(new PagingQuery { Skip = 0, Take = 500 }, 500, 500);
            using var scope = DatabaseFixture.Factory.Services.CreateScope();

            var ofThePost = (await Repository(scope).GetAsync(PostId, paging))
                .Select(r => r.Id).Where(ours.Contains).ToArray();
            ofThePost.Should().Equal([high, middle, low],
                "the moment is a tie, and the identifier is what decides it, the same on every read");

            var ofTheGame = (await Repository(scope).GetAllAsync(paging,
                    new PostReviewFilter { GameId = TestConstants.TestGameId }))
                .Select(r => r.Id).Where(ours.Contains).ToArray();
            ofTheGame.Should().Equal([high, middle, low],
                "the lists of a profile and of moderation page by the same order");
        }
        finally
        {
            foreach (var (id, _) in written)
            {
                await PostReviewTestHelper.RemoveReview(DatabaseFixture, id);
            }
        }
    }

    /// <summary>One removal on a scope of its own, as a request would have.</summary>
    private Task Remove(Guid reviewId, Guid? removedBy = null) => Task.Run(async () =>
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        await Repository(scope).UpdateAsync(
            new UpdatePostReviewEntity(
                reviewId,
                IsRemoved: true,
                DeletedUtc: DateTimeOffset.UtcNow,
                DeletedByUserId: removedBy ?? RaterId));
    });
}
