using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbPostReview = DM.Infrastructure.Persistence.Entities.Game.PostReview;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Editing and removing a post review, and the two numbers that have to agree
/// with the result.
/// </summary>
/// <remarks>
/// A review carries a sign, and the sign is counted twice: into the post's
/// rating, which the rated-posts listing sums over the surviving rows, and into
/// the post author's quality rating, which is a stored counter moved by deltas.
/// The first cannot drift by construction; the second can, and every case here
/// that changes a sign asserts the delta rather than an absolute number — the
/// counter belongs to a user the rest of the fixture also writes to.
///
/// The reviews are seeded directly. Creating them through the API needs a post
/// in an accessible room, a hundred game posts behind the author and a
/// three-day gap since their last review in the game, none of which this file
/// is about.
/// </remarks>
public class PostReviewLifecycleShould : IntegrationTestBase
{
    public PostReviewLifecycleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>The post every review here lands on, and the user who wrote it.</summary>
    private static Guid PostId => TestConstants.TestGamePostId;

    private static Guid PostAuthorId => TestConstants.TestUserId;

    private static string ReviewUrl(Guid reviewId) => $"/v1/posts/{PostId}/reviews/{reviewId}";

    private static GeneralUser AsUser(Guid userId, string username, UserRole role) =>
        new() { UserId = userId, Username = username, Role = role };

    private static GeneralUser Mentor => AsUser(
        TestConstants.MentorUserId, TestConstants.MentorUserUsername, UserRole.Mentor);

    private static GeneralUser Moderator => AsUser(
        TestConstants.ModeratorUserId, TestConstants.ModeratorUserUsername, UserRole.Moderator);

    private static GeneralUser SeniorModerator => AsUser(
        TestConstants.SeniorModeratorUserId, TestConstants.SeniorModeratorUserUsername,
        UserRole.SeniorModerator);

    /// <summary>
    /// Seed one review of the fixture's game post.
    /// </summary>
    /// <param name="authorId">Who wrote it.</param>
    /// <param name="sign">Its rating impact.</param>
    /// <param name="ageMinutes">How long ago, which is what decides the edit window.</param>
    private async Task<Guid> SeedReview(Guid authorId, short sign, int ageMinutes = 0)
    {
        var reviewId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.PostReviews.Add(new DbPostReview
        {
            PostReviewId = reviewId,
            AuthorId = authorId,
            PostId = PostId,
            PostAuthorId = PostAuthorId,
            GameId = TestConstants.TestGameId,
            CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-ageMinutes),
            Text = "Исходный текст оценки",
            SignValue = sign,
            IsRemoved = false
        });
        await db.SaveChangesAsync();
        return reviewId;
    }

    /// <summary>
    /// IgnoreQueryFilters: a row this suite has just had removed is hidden by
    /// the global soft-delete filter, and the cleanup would leave it behind.
    /// </summary>


    /// <summary>The counter a received review moves.</summary>

    /// <summary>
    /// Put the user past probation, so a sign may be changed at all, and answer
    /// with what to set it back to.
    /// </summary>
    private async Task<int> PastProbation(Guid userId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        var previous = user.QuantityRating;
        user.QuantityRating = 500;
        await db.SaveChangesAsync();
        return previous;
    }

    private async Task RestoreQuantityRating(Guid userId, int value)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var user = await db.Users.FirstAsync(u => u.UserId == userId);
        user.QuantityRating = value;
        await db.SaveChangesAsync();
    }

    /// <summary>The rating and the review count the rated listing reports for the post.</summary>
    private async Task<(int Rating, int ReviewCount)> ListedPost()
    {
        var response = await Client.GetAsync(
            $"/v1/posts?gameId={TestConstants.TestGameId}&hasReviews=false&take=100");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var post = document.RootElement.GetProperty("resources").EnumerateArray()
            .Single(p => p.GetProperty("id").GetGuid() == PostId);
        return (post.GetProperty("rating").GetInt32(), post.GetProperty("reviewCount").GetInt32());
    }

    private async Task<HttpResponseMessage> Patch(Guid reviewId, GeneralUser user, object body)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, ReviewUrl(reviewId), user);
        request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> Delete(Guid reviewId, GeneralUser user) =>
        await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Delete, ReviewUrl(reviewId), user));

    [Fact]
    public async Task LetTheAuthorCorrectTheirOwnWordsInsideTheWindow()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            var response = await Patch(reviewId, Moderator, new { text = "Перечитал, поправил формулировку" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var stored = await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId);
            stored!.Text.Should().Be("Перечитал, поправил формулировку");
            stored.SignValue.Should().Be(1, "an edit of the wording is not a change of the rating");
            stored.ModifiedByUserId.Should().Be(Moderator.UserId);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task MoveBothRatingsWhenTheAuthorFlipsTheSign()
    {
        var probation = await PastProbation(Moderator.UserId);
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            var (ratingBefore, countBefore) = await ListedPost();
            var qualityBefore = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);

            var response = await Patch(reviewId, Moderator, new { sign = "Negative" });
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var (ratingAfter, countAfter) = await ListedPost();
            // +1 taken back and -1 applied: the post's rating is a sum over the
            // surviving rows, the author's counter is moved by two deltas.
            ratingAfter.Should().Be(ratingBefore - 2);
            countAfter.Should().Be(countBefore, "an edit does not add or remove a review");
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(qualityBefore - 2);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQuantityRating(Moderator.UserId, probation);
        }
    }

    [Fact]
    public async Task RefuseASignThatIsNotOneOfTheThree()
    {
        var probation = await PastProbation(Moderator.UserId);
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            var (ratingBefore, countBefore) = await ListedPost();
            var qualityBefore = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);

            // The enum converter accepts integers, so the body binds and the
            // update path adds the sign to a public counter as a number. Without
            // the range rule this is an arbitrary amount of somebody else's
            // quality rating, handed out inside the author's own edit window.
            var response = await Patch(reviewId, Moderator, new { sign = 30000 });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var (ratingAfter, countAfter) = await ListedPost();
            ratingAfter.Should().Be(ratingBefore);
            countAfter.Should().Be(countBefore);
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(qualityBefore);
            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.SignValue.Should().Be(1);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
            await RestoreQuantityRating(Moderator.UserId, probation);
        }
    }

    [Fact]
    public async Task RefuseAnEditFromSomebodyElse()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            // A senior moderator may take this review down and still may not
            // put different words under its author's name.
            var stranger = await Patch(reviewId, SeniorModerator, new { text = "Чужая правка" });
            stranger.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.Text.Should().Be("Исходный текст оценки");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task RefuseAnEditOnceTheWindowHasClosed()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1, ageMinutes: 20);
        try
        {
            var response = await Patch(reviewId, Moderator, new { text = "Поздняя правка" });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.Text.Should().Be("Исходный текст оценки");
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task LetTheAuthorDeleteLongAfterTheWindowHasClosed()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1, ageMinutes: 60 * 24 * 30);
        try
        {
            var response = await Delete(reviewId, Moderator);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeTrue();
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task LetASeniorModeratorRemoveSomebodyElsesReviewAndSettleBothRatings()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            var (ratingBefore, countBefore) = await ListedPost();
            var qualityBefore = await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId);

            var response = await Delete(reviewId, SeniorModerator);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var (ratingAfter, countAfter) = await ListedPost();
            ratingAfter.Should().Be(ratingBefore - 1);
            countAfter.Should().Be(countBefore - 1);
            (await PostReviewTestHelper.QualityRatingOf(DatabaseFixture, PostAuthorId)).Should().Be(qualityBefore - 1);

            var stored = await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId);
            stored!.IsRemoved.Should().BeTrue();
            // Whose hand it was: a removal of somebody else's words with nobody
            // recorded cannot be reviewed afterwards.
            stored.DeletedByUserId.Should().Be(SeniorModerator.UserId);
            stored.DeletedUtc.Should().NotBeNull();

            var listed = await Client.GetAsync($"/v1/posts/{PostId}/reviews");
            listed.StatusCode.Should().Be(HttpStatusCode.OK);
            (await listed.Content.ReadAsStringAsync()).Should().NotContain(reviewId.ToString());
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task RefuseADeleteFromARankBelowSeniorModeration()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            // A mentor curates games and a moderator holds the forum; neither
            // rank reaches a stated opinion and the rating riding on it.
            (await Delete(reviewId, Mentor)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await PostReviewTestHelper.StoredReview(DatabaseFixture, reviewId))!.IsRemoved.Should().BeFalse();
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }

    [Fact]
    public async Task RefuseBothCallsFromAVisitor()
    {
        var reviewId = await SeedReview(Moderator.UserId, sign: 1);
        try
        {
            var edit = await Client.PatchAsync(
                ReviewUrl(reviewId), JsonContent.Create(new { text = "Аноним" }));
            var delete = await Client.DeleteAsync(ReviewUrl(reviewId));

            edit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await PostReviewTestHelper.RemoveReview(DatabaseFixture, reviewId);
        }
    }
}
