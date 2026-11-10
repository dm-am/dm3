using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Game.Features.PostReviews;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Web.API.IntegrationTests.Repositories;

/// <remarks>
/// QualityRating is a stored column nothing recomputes. Against the container Postgres: the rollback is under test,
/// forced by a foreign key on the modifier after the counter moved inside the transaction.
/// </remarks>
public class PostReviewRepositoryShould : IntegrationTestBase
{
    public PostReviewRepositoryShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private static Guid PostId => TestConstants.TestGamePostId;

    private static Guid PostAuthorId => TestConstants.TestUserId;

    // Whoever writes the reviews below; not the post's author.
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

    // The counter is shared with the rest of the fixture, so put it back.
    private async Task RestoreQualityRating(Guid userId, int value)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        user.QualityRating = value;
        await db.SaveChangesAsync();
    }

    private static IPostReviewRepository Repository(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IPostReviewRepository>();

    private async Task WithSeededReview(short sign, Func<Guid, int, Task> test)
    {
        var reviewId = await SeedReview(sign);
        int? before = null;
        try
        {
            before = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);
            await test(reviewId, before.Value);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            if (before is { } rating)
            {
                await RestoreQualityRating(PostAuthorId, rating);
            }
        }
    }

    [Fact]
    public async Task LeaveTheCounterWhereItWasWhenTheRowWriteIsRefused()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
        {
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
        });
    }

    [Fact]
    public async Task MoveTheCounterWithTheSignItPaysFor()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
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
        });
    }

    [Fact]
    public async Task GiveTheSignBackWithTheRemovalAndNotBeforeIt()
    {
        await WithSeededReview(sign: -1, async (reviewId, before) =>
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
        });
    }

    [Fact]
    public async Task PayNothingForAReviewTheUniqueIndexRefuses()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
        {
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
                });

            await act.Should().ThrowAsync<DuplicateEntityException>();

            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before);
        });
    }

    // What the counter owes is read off the row; a scope apiece and a timeout on the pair.
    [Fact]
    public async Task GiveTheSignBackOnceWhenTwoRemovalsRaceForOneReview()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
        {
            await Task.WhenAll(Remove(reviewId), Remove(reviewId))
                .WaitAsync(TimeSpan.FromSeconds(30));

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before - 1,
                "the plus is given back by the removal that happened, and the one that " +
                "found the review already gone owes nothing");
        });
    }

    // A removed review is worth nothing, which only a write reading the row itself can see.
    [Fact]
    public async Task MoveNothingForAnEditQueuedBehindTheRemoval()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
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
        });
    }

    // An edit of the text carries no sign, and the counter owes the difference of contributions.
    [Fact]
    public async Task KeepTheStoredSignWhenTheRequestCarriesOnlyText()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
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
        });
    }

    // The contribution is read off the row, not guessed from the review's existence.
    [Fact]
    public async Task GiveNothingBackForTheRemovalOfANeutralReview()
    {
        await WithSeededReview(sign: 0, async (reviewId, before) =>
        {
            await Remove(reviewId);

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(before,
                "the review was worth nothing while it stood, so there is nothing to give back");
        });
    }

    // The second press finds the row removed, and the audit columns hold still.
    [Fact]
    public async Task KeepTheFirstRemoversHandWhenTheReviewIsRemovedAgain()
    {
        await WithSeededReview(sign: 1, async (reviewId, before) =>
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
        });
    }

    // Reviews of one post written in one moment come back in one order on every read: the list
    // numbers them by it.
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

    // One removal on a scope of its own, as a request would have.
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
