using System.Net;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Personal.Features.Profiles;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Integration tests for UserGameReviewController and the two profile counters
/// it backs.
/// </summary>
/// <remarks>
/// A game review is authored by one user and lands on another — the master of
/// the reviewed game — so every assertion here is about which of the two names
/// a row answers to. The seeded fixture gives the two sides different owners:
/// TestGame is mastered by TestUser, SecondGame by SecondUser.
///
/// Counters are asserted as deltas rather than as absolute numbers: the profile
/// counts every review in the database, and a test that pinned the total would
/// be a test about what else the fixture happens to contain.
///
/// The arithmetic is read off the repository rather than off GET
/// /v1/users/{name}/profile: that endpoint answers out of
/// CommunityProfileService's cache, so a before-and-after pair taken through it
/// measures the cache and not the count. What the endpoint is held to here is
/// the other half — that both numbers reach the wire at all.
/// </remarks>
public class UserGameReviewControllerShould : IntegrationTestBase
{
    public UserGameReviewControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Seed a review directly: the create endpoint requires a post in the game
    /// and a hundred game posts overall, which is too heavy to arrange through
    /// the API.
    /// </summary>
    private async Task SeedReview(Guid id, Guid authorId, Guid gameId, string text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.GameReviews.Add(new DbGameReview
        {
            GameReviewId = id,
            AuthorId = authorId,
            GameId = gameId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            Text = text,
            IsRemoved = false
        });
        await db.SaveChangesAsync();
    }

    private async Task RemoveReviews(params Guid[] ids)
    {
        // List<T>.Contains: the array's ReadOnlySpan Contains overload is not
        // translatable by EF's parameter extraction.
        // IgnoreQueryFilters: the global soft-delete filter hides a row this
        // suite has just marked removed, and the cleanup would leave it behind.
        var idList = ids.ToList();
        await using var db = DatabaseFixture.CreateDbContext();
        await db.GameReviews.IgnoreQueryFilters()
            .Where(r => idList.Contains(r.GameReviewId)).ExecuteDeleteAsync();
    }

    /// <summary>Review ids the listing at <paramref name="url" /> returned.</summary>
    private async Task<List<Guid>> ReviewIdsAt(string url)
    {
        var response = await Client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resources").EnumerateArray()
            .Select(r => r.GetProperty("id").GetGuid())
            .ToList();
    }

    /// <summary>
    /// The counted user, straight off the repository the profile is built from.
    /// Uncached, so a second read after a write sees the write.
    /// </summary>
    private async Task<GeneralUser> Counted(string username)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();

        var user = await users.GetUserAsync(username);
        user.Should().NotBeNull($"the fixture seeds {username}");
        return user!;
    }

    [Fact]
    public async Task ListOnlyTheReviewsOfTheGamesTheUserMasters()
    {
        // Arrange - one review of TestUser's game, two of somebody else's
        var received = Guid.NewGuid();
        var writtenBySameUser = Guid.NewGuid();
        var strangers = Guid.NewGuid();
        await SeedReview(received, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Мастер ведет игру ровно, темп не проседает");
        await SeedReview(writtenBySameUser, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Интересная завязка, но описания скупые");
        await SeedReview(strangers, TestConstants.ModeratorUserId, TestConstants.SecondGameId,
            "Партия развалилась на середине");

        try
        {
            // Act
            var ids = await ReviewIdsAt($"/v1/users/{TestConstants.TestUserLogin}/game-reviews");

            // Assert - a review of somebody else's game is somebody else's,
            // whoever wrote it
            ids.Should().Contain(received);
            ids.Should().NotContain(writtenBySameUser);
            ids.Should().NotContain(strangers);
        }
        finally
        {
            await RemoveReviews(received, writtenBySameUser, strangers);
        }
    }

    [Fact]
    public async Task ListOnlyTheReviewsTheUserWrote()
    {
        // Arrange
        var written = Guid.NewGuid();
        var aboutTheSameUsersGame = Guid.NewGuid();
        await SeedReview(written, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Отыграно бодро, мастер держит слово");
        await SeedReview(aboutTheSameUsersGame, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Сеттинг проработан, а вот с ритмом беда");

        try
        {
            // Act
            var ids = await ReviewIdsAt($"/v1/users/{TestConstants.TestUserLogin}/written-game-reviews");

            // Assert - authorship, not the game
            ids.Should().Contain(written);
            ids.Should().NotContain(aboutTheSameUsersGame);
        }
        finally
        {
            await RemoveReviews(written, aboutTheSameUsersGame);
        }
    }

    [Fact]
    public async Task NameTheReviewedGame()
    {
        // Arrange - the user-level listing is the one place where the game is
        // what differs between rows, and the id alone is not a name
        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Хороший модуль, но мало игроков");

        try
        {
            // Act
            var response = await Client.GetAsync(
                $"/v1/users/{TestConstants.TestUserLogin}/written-game-reviews");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            // Assert
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var review = document.RootElement.GetProperty("resources").EnumerateArray()
                .Single(r => r.GetProperty("id").GetGuid() == reviewId);
            review.GetProperty("gameTitle").GetString().Should().Be("Second Test Game");
        }
        finally
        {
            await RemoveReviews(reviewId);
        }
    }

    [Fact]
    public async Task CountAReceivedReviewForTheGameMasterAndNobodyElse()
    {
        // Arrange
        var before = (await Counted(TestConstants.TestUserLogin)).GameReviewsReceivedCount;
        var authorBefore = (await Counted(TestConstants.SecondUserLogin)).GameReviewsReceivedCount;

        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Мастер отвечает быстро, сцены живые");

        try
        {
            // Act / Assert - the review lands on the master of the reviewed
            // game; its author has received nothing
            (await Counted(TestConstants.TestUserLogin)).GameReviewsReceivedCount
                .Should().Be(before + 1);
            (await Counted(TestConstants.SecondUserLogin)).GameReviewsReceivedCount
                .Should().Be(authorBefore);
        }
        finally
        {
            await RemoveReviews(reviewId);
        }
    }

    [Fact]
    public async Task CountAWrittenReviewForItsAuthorAndNobodyElse()
    {
        // Arrange
        var before = (await Counted(TestConstants.TestUserLogin)).GameReviewsGivenCount;
        var masterBefore = (await Counted(TestConstants.SecondUserLogin)).GameReviewsGivenCount;

        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Ровная кампания без провисаний");

        try
        {
            // Act / Assert
            (await Counted(TestConstants.TestUserLogin)).GameReviewsGivenCount
                .Should().Be(before + 1);
            (await Counted(TestConstants.SecondUserLogin)).GameReviewsGivenCount
                .Should().Be(masterBefore);
        }
        finally
        {
            await RemoveReviews(reviewId);
        }
    }

    [Fact]
    public async Task CountNeitherSideOfARemovedReview()
    {
        // Arrange - a deleted review is gone from both numbers, not just from
        // the list
        var receivedBefore = (await Counted(TestConstants.TestUserLogin)).GameReviewsReceivedCount;
        var givenBefore = (await Counted(TestConstants.SecondUserLogin)).GameReviewsGivenCount;

        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Отозванная рецензия, ее не должно быть видно");

        try
        {
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var review = await db.GameReviews.FirstAsync(r => r.GameReviewId == reviewId);
                review.IsRemoved = true;
                await db.SaveChangesAsync();
            }

            // Act / Assert
            (await Counted(TestConstants.TestUserLogin)).GameReviewsReceivedCount
                .Should().Be(receivedBefore);
            (await Counted(TestConstants.SecondUserLogin)).GameReviewsGivenCount
                .Should().Be(givenBefore);
            (await ReviewIdsAt($"/v1/users/{TestConstants.TestUserLogin}/game-reviews"))
                .Should().NotContain(reviewId);
        }
        finally
        {
            await RemoveReviews(reviewId);
        }
    }

    /// <summary>
    /// The other half of the counter: whatever the number is, the profile
    /// endpoint publishes it. The arithmetic is asserted above, off the
    /// uncached repository; this is about the two fields existing on the wire,
    /// which is what the profile page reads.
    /// </summary>
    [Fact]
    public async Task PublishBothCountersOnTheProfile()
    {
        var response = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var profile = document.RootElement.GetProperty("resource");

        profile.GetProperty("gameReviewsReceived").GetInt32().Should().BeGreaterThanOrEqualTo(0);
        profile.GetProperty("gameReviewsGiven").GetInt32().Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AnswerNotFoundForAUserThatDoesNotExist()
    {
        var response = await Client.GetAsync("/v1/users/nosuchpersonatall/game-reviews");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
