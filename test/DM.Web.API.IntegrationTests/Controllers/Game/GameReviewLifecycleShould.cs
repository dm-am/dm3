using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbGameReview = DM.Infrastructure.Persistence.Entities.Game.GameReview;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// Editing and removing a game review, through the same rules the post review
/// and the endorsement answer by.
/// </summary>
/// <remarks>
/// The service has long known how to edit and remove a game review, and nothing
/// on the wire reached it: a review, once written, stayed as written. The rules are
/// the service's own and are only asked here: the author edits inside a day of
/// publication, the author or a senior moderator removes at any time, and anybody
/// else is refused.
///
/// The reviews are seeded directly, each under an author made for it. Creating them
/// through the API needs a post in the game and a hundred game posts behind the
/// author, and one live review per author and game is a unique index, so a seeded
/// author of the fixture would collide with the review files next door.
/// </remarks>
public class GameReviewLifecycleShould : IntegrationTestBase
{
    public GameReviewLifecycleShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    private const string OriginalText = "Исходный текст рецензии на игру";

    private static string ReviewUrl(Guid reviewId) =>
        $"/v1/games/{TestConstants.TestGameId}/reviews/{reviewId}";

    private static GeneralUser AsUser(Guid userId, string username, UserRole role) =>
        new() { UserId = userId, Username = username, Role = role };

    private static GeneralUser Moderator => AsUser(
        TestConstants.ModeratorUserId, TestConstants.ModeratorUserUsername, UserRole.Moderator);

    private static GeneralUser SeniorModerator => AsUser(
        TestConstants.SeniorModeratorUserId, TestConstants.SeniorModeratorUserUsername,
        UserRole.SeniorModerator);

    private static GeneralUser Stranger => CustomWebApplicationFactory.CreateSecondUser();

    [Fact]
    public async Task LetTheAuthorCorrectTheirOwnWordsInsideTheWindow()
    {
        var (author, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            var response = await Patch(reviewId, author, new { text = "Перечитал и поправил рецензию" });

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var resource = document.RootElement.GetProperty("resource");
            resource.GetProperty("id").GetGuid().Should().Be(reviewId);
            resource.GetProperty("modifiedUtc").ValueKind.Should().NotBe(JsonValueKind.Null);

            var stored = await StoredAsync(reviewId);
            stored!.Text.Should().Be("Перечитал и поправил рецензию");
            stored.ModifiedByUserId.Should().Be(author.UserId);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task RefuseAnEditOnceTheDayHasPassed()
    {
        var (author, reviewId) = await SeedAsync(ageHours: 25);
        try
        {
            var response = await Patch(reviewId, author, new { text = "Поздняя правка рецензии" });

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await StoredAsync(reviewId))!.Text.Should().Be(OriginalText);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task RefuseAnEditFromSomebodyElse()
    {
        var (_, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            // A senior moderator may take this review down and still may not put
            // different words under its author's name.
            (await Patch(reviewId, SeniorModerator, new { text = "Чужая правка рецензии" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Patch(reviewId, Stranger, new { text = "Чужая правка рецензии" }))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await StoredAsync(reviewId))!.Text.Should().Be(OriginalText);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task RefuseATextCreationWouldHaveRefused()
    {
        var (author, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            var response = await Patch(reviewId, author, new { text = "Коротко" });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await StoredAsync(reviewId))!.Text.Should().Be(OriginalText);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    /// <summary>
    /// A text of nothing but spaces passes the length of the request and is
    /// trimmed to nothing before it is stored, so the domain has to refuse it the
    /// way creation does: an edit must not publish an empty review.
    /// </summary>
    [Fact]
    public async Task RefuseATextOfNothingButSpaces()
    {
        var (author, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            var response = await Patch(reviewId, author, new { text = new string(' ', 12) });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await StoredAsync(reviewId))!.Text.Should().Be(OriginalText);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    /// <summary>
    /// The editor opens a review with the audience that hands its author the text
    /// as typed. The display rendering turns "--" into a dash entity the editor
    /// does not turn back, and saving what it was handed wrote the entity into the
    /// review as text.
    /// </summary>
    [Fact]
    public async Task HandTheAuthorTheTextAsTypedForEditing()
    {
        const string typed = "Раз -- два --- три, и так на все двадцать знаков";
        var (author, reviewId) = await SeedAsync(ageHours: 1, text: typed);
        try
        {
            var forEditing = CreateAuthenticatedRequest(HttpMethod.Get, ReviewUrl(reviewId), author);
            forEditing.Headers.Add(BbAudienceHeader.HeaderName, "author_edit");

            (await TextOf(await Client.SendAsync(forEditing))).Should().Be(BbParserWrapper.LineBreaksWritten + typed);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    /// <summary>
    /// The same request from anybody but the author is the ordinary rendering:
    /// the audience is a header any caller can send, and the source is the
    /// author's own view.
    /// </summary>
    [Fact]
    public async Task RenderTheReviewForAnybodyElseWhoAsksForTheSource()
    {
        var (_, reviewId) = await SeedAsync(ageHours: 1, text: "Раз -- два, и так далее");
        try
        {
            var forEditing = CreateAuthenticatedRequest(HttpMethod.Get, ReviewUrl(reviewId), Stranger);
            forEditing.Headers.Add(BbAudienceHeader.HeaderName, "author_edit");

            (await TextOf(await Client.SendAsync(forEditing))).Should().Be("Раз &ndash; два, и так далее");
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task LetTheAuthorDeleteLongAfterTheWindowHasClosed()
    {
        var (author, reviewId) = await SeedAsync(ageHours: 24 * 30);
        try
        {
            var response = await Delete(reviewId, author);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var stored = await StoredAsync(reviewId);
            stored!.IsRemoved.Should().BeTrue();
            stored.DeletedByUserId.Should().Be(author.UserId);
            stored.DeletedUtc.Should().NotBeNull();

            (await Client.GetAsync(ReviewUrl(reviewId))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task LetASeniorModeratorRemoveSomebodyElsesReview()
    {
        var (_, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            var response = await Delete(reviewId, SeniorModerator);

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var stored = await StoredAsync(reviewId);
            stored!.IsRemoved.Should().BeTrue();
            // Whose hand it was: a removal of somebody else's words with nobody
            // recorded cannot be reviewed afterwards.
            stored.DeletedByUserId.Should().Be(SeniorModerator.UserId);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    [Fact]
    public async Task RefuseADeleteFromSomebodyBelowSeniorModeration()
    {
        var (_, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            (await Delete(reviewId, Moderator)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await Delete(reviewId, Stranger)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

            (await StoredAsync(reviewId))!.IsRemoved.Should().BeFalse();
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    /// <summary>
    /// A review that is not there answers the way the post review and the
    /// endorsement answer: 404, where somebody else's answers 403. The review
    /// itself is public, so the difference tells a caller nothing it could not read.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundOnAReviewThatIsNotThere()
    {
        var author = CustomWebApplicationFactory.CreateTestUser();
        var missing = Guid.NewGuid();

        (await Patch(missing, author, new { text = "Правка несуществующей рецензии" }))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Delete(missing, author)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RefuseBothCallsFromAVisitor()
    {
        var (_, reviewId) = await SeedAsync(ageHours: 1);
        try
        {
            var edit = await Client.PatchAsync(
                ReviewUrl(reviewId), JsonContent.Create(new { text = "Анонимная правка рецензии" }));
            var delete = await Client.DeleteAsync(ReviewUrl(reviewId));

            edit.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            delete.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await DropAsync(reviewId);
        }
    }

    /// <summary>
    /// One review of the fixture's game, written <paramref name="ageHours" /> ago
    /// by an author made for it.
    /// </summary>
    private async Task<(GeneralUser Author, Guid ReviewId)> SeedAsync(int ageHours, string text = OriginalText)
    {
        var userId = Guid.NewGuid();
        var reviewId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        var user = new DbUser
        {
            UserId = userId,
            // Username is varchar(20) and uniquely indexed, so the id pads the prefix.
            Username = $"grl{userId:N}"[..20],
            Email = $"{userId:N}@gamereviewlifecycle.example",
            PasswordHash = "hash",
            Salt = "salt"
        };
        db.Users.Add(user);
        db.GameReviews.Add(new DbGameReview
        {
            GameReviewId = reviewId,
            AuthorId = userId,
            GameId = TestConstants.TestGameId,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-ageHours),
            Text = text,
            IsRemoved = false
        });
        await db.SaveChangesAsync();
        return (AsUser(userId, user.Username, UserRole.RegularUser), reviewId);
    }

    /// <summary>
    /// IgnoreQueryFilters: a row this suite has just had removed is hidden by the
    /// global soft-delete filter.
    /// </summary>
    private async Task<DbGameReview?> StoredAsync(Guid reviewId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.GameReviews.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(r => r.GameReviewId == reviewId);
    }

    private async Task DropAsync(Guid reviewId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.GameReviews.IgnoreQueryFilters()
            .Where(r => r.GameReviewId == reviewId).ExecuteDeleteAsync();
    }

    private async Task<HttpResponseMessage> Patch(Guid reviewId, GeneralUser user, object body)
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Patch, ReviewUrl(reviewId), user);
        request.Content = JsonContent.Create(body);
        return await Client.SendAsync(request);
    }

    private static async Task<string> TextOf(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("resource").GetProperty("text").GetString()!;
    }

    private async Task<HttpResponseMessage> Delete(Guid reviewId, GeneralUser user) =>
        await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Delete, ReviewUrl(reviewId), user));
}
