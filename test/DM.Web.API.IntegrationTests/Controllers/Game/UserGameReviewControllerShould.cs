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
/// The game reviews and the two profile counters they back.
/// </summary>
/// <remarks>
/// TestGame is mastered by TestUser and SecondGame by SecondUser. Counters are asserted as deltas off the
/// repository: the profile endpoint answers out of a cache.
/// </remarks>
public class UserGameReviewControllerShould : IntegrationTestBase
{
    public UserGameReviewControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Seed a review directly: the create endpoint's gates are too heavy to arrange through the API.
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
        // One review of TestUser's game, two of somebody else's
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
            var ids = await ReviewIdsAt($"/v1/users/{TestConstants.TestUserLogin}/game-reviews");

            // A review of somebody else's game is somebody else's,
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
        var written = Guid.NewGuid();
        var aboutTheSameUsersGame = Guid.NewGuid();
        await SeedReview(written, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Отыграно бодро, мастер держит слово");
        await SeedReview(aboutTheSameUsersGame, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Сеттинг проработан, а вот с ритмом беда");

        try
        {
            var ids = await ReviewIdsAt($"/v1/users/{TestConstants.TestUserLogin}/written-game-reviews");

            // Authorship, not the game
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
        // The user-level listing is the one place where the game is
        // what differs between rows, and the id alone is not a name
        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Хороший модуль, но мало игроков");

        try
        {
            var response = await Client.GetAsync(
                $"/v1/users/{TestConstants.TestUserLogin}/written-game-reviews");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

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
        var before = (await Counted(TestConstants.TestUserLogin)).GameReviewsReceivedCount;
        var authorBefore = (await Counted(TestConstants.SecondUserLogin)).GameReviewsReceivedCount;

        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.SecondUserId, TestConstants.TestGameId,
            "Мастер отвечает быстро, сцены живые");

        try
        {
            // The review lands on the master of the reviewed
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
        var before = (await Counted(TestConstants.TestUserLogin)).GameReviewsGivenCount;
        var masterBefore = (await Counted(TestConstants.SecondUserLogin)).GameReviewsGivenCount;

        var reviewId = Guid.NewGuid();
        await SeedReview(reviewId, TestConstants.TestUserId, TestConstants.SecondGameId,
            "Ровная кампания без провисаний");

        try
        {
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
        // A deleted review is gone from both numbers, not just from
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
    /// The profile endpoint publishes both counters.
    /// </summary>
    [Fact]
    public async Task PublishBothCountersOnTheProfile()
    {
        var response = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}/profile");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var profile = document.RootElement.GetProperty("resource");

        profile.GetProperty("gameReviewsReceived").ValueKind.Should().Be(JsonValueKind.Number);
        profile.GetProperty("gameReviewsGiven").ValueKind.Should().Be(JsonValueKind.Number);
    }

    [Fact]
    public async Task AnswerNotFoundForAUserThatDoesNotExist()
    {
        var response = await Client.GetAsync("/v1/users/nosuchpersonatall/game-reviews");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
