using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Configuration;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbUserEndorsement = DM.Infrastructure.Persistence.Entities.Community.UserEndorsement;

namespace DM.Web.API.IntegrationTests.Controllers.Community;

/// <summary>
/// Integration tests for UserEndorsementController
/// </summary>
public class UserEndorsementControllerShould : IntegrationTestBase
{
    public UserEndorsementControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Seed an endorsement directly: the create endpoint requires the pair
    /// to have played together and the author to have 100+ game posts,
    /// which is too heavy to arrange through the API.
    /// </summary>
    private async Task SeedEndorsement(Guid id, Guid authorId, Guid targetUserId, string text, DateTimeOffset createdUtc)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.UserEndorsements.Add(new DbUserEndorsement
        {
            UserEndorsementId = id,
            AuthorId = authorId,
            TargetUserId = targetUserId,
            CreatedUtc = createdUtc,
            Text = text,
            IsRemoved = false
        });
        await db.SaveChangesAsync();
    }

    private async Task RemoveEndorsements(params Guid[] ids)
    {
        // List<T>.Contains: the array's ReadOnlySpan Contains overload is
        // not translatable by EF's parameter extraction
        var idList = ids.ToList();
        await using var db = DatabaseFixture.CreateDbContext();
        await db.UserEndorsements.Where(e => idList.Contains(e.UserEndorsementId)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task GetUserEndorsements_ReturnsTextAsPlainString()
    {
        // Arrange - endorsement text is plain text by contract (owner
        // decision): BBCode-looking markup must come back verbatim,
        // without server-side HTML rendering
        var endorsementId = Guid.NewGuid();
        const string text = "Отличный игрок, [b]рекомендую[/b] без оговорок";
        await SeedEndorsement(
            endorsementId,
            TestConstants.TestUserId,
            TestConstants.SecondUserId,
            text,
            DateTimeOffset.UtcNow.AddHours(-1));

        try
        {
            // Act
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/endorsements");

            // Assert - text round-trips unchanged
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var endorsement = doc.RootElement.GetProperty("resources").EnumerateArray()
                .Single(e => e.GetProperty("id").GetGuid() == endorsementId);
            endorsement.GetProperty("text").GetString().Should().Be(text);
        }
        finally
        {
            await RemoveEndorsements(endorsementId);
        }
    }

    [Fact]
    public async Task GetWrittenEndorsements_SortByAuthor_OrdersByTargetUsername()
    {
        // Arrange - TestUser wrote about moderator / mentor / admin; creation
        // order is reverse-alphabetical by target so a created-date fallback
        // (including the old Author.Username sort, where the author is
        // constant and the tie-breaker decides) yields a different sequence
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var now = DateTimeOffset.UtcNow;
        await SeedEndorsement(ids[0], TestConstants.TestUserId, TestConstants.ModeratorUserId, "Надежный игрок", now.AddHours(-3));
        await SeedEndorsement(ids[1], TestConstants.TestUserId, TestConstants.MentorUserId, "Отличный рассказчик", now.AddHours(-2));
        await SeedEndorsement(ids[2], TestConstants.TestUserId, TestConstants.AdminUserId, "Хороший мастер", now.AddHours(-1));

        try
        {
            // Act
            var response = await Client.GetAsync(
                $"/v1/users/{TestConstants.TestUserLogin}/written-endorsements?sortBy=author&sortOrder=asc");

            // Assert - sorted alphabetically by target username
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var usernames = doc.RootElement.GetProperty("resources").EnumerateArray()
                .Select(e => e.GetProperty("targetUser").GetProperty("username").GetString())
                .ToList();
            usernames.Should().ContainInOrder(
                TestConstants.AdminUserLogin,
                TestConstants.MentorUserLogin,
                TestConstants.ModeratorUserLogin);
        }
        finally
        {
            await RemoveEndorsements(ids);
        }
    }

    [Fact]
    public async Task GetUserEndorsements_WithEqualCreatedUtc_TieBreaksById()
    {
        // Arrange - identical CreatedUtc; the row with the greater id is
        // inserted first so insertion order cannot masquerade as the
        // deterministic id tie-breaker
        var firstId = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
        var secondId = Guid.Parse("00000000-0000-0000-0000-0000000000e2");
        var createdUtc = DateTimeOffset.UtcNow.AddDays(-1);
        await SeedEndorsement(secondId, TestConstants.AdminUserId, TestConstants.SecondUserId, "Прекрасно отыгрывает", createdUtc);
        await SeedEndorsement(firstId, TestConstants.ModeratorUserId, TestConstants.SecondUserId, "Всегда пишет в срок", createdUtc);

        try
        {
            // Act
            var response = await Client.GetAsync(
                $"/v1/users/{TestConstants.SecondUserLogin}/endorsements?sortBy=created&sortOrder=asc");

            // Assert - equal CreatedUtc resolves to ascending id order
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var returnedIds = doc.RootElement.GetProperty("resources").EnumerateArray()
                .Select(e => e.GetProperty("id").GetGuid())
                .ToList();
            returnedIds.Should().ContainInOrder(firstId, secondId);
        }
        finally
        {
            await RemoveEndorsements(firstId, secondId);
        }
    }

    [Fact]
    public async Task GetUserEndorsements_WithValidUser_ReturnsOk()
    {
        // Act - route is /v1/users/{username}/endorsements
        var response = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}/endorsements");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// The post that makes SecondUser and TestUser "players of the same game":
    /// TestUser already has one in TestRoom, this is the other half of the
    /// pair. Removed again by <see cref="RevokeEndorsementEligibility" />.
    /// </summary>
    private static readonly Guid EligibilityPostId = Guid.Parse("00000000-0000-0000-0000-0000000000e5");

    /// <summary>
    /// Put the author past every gate of the create endpoint that is not the
    /// rule under test: out of probation, and with a post in the room where
    /// the recipient already has one.
    /// </summary>
    /// <returns>The author's previous post counter, to put back afterwards.</returns>
    private async Task<int> GrantEndorsementEligibility()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var author = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
        var previousRating = author.QuantityRating;
        author.QuantityRating = ProbationPolicy.NewbiePostThreshold;

        if (!await db.Set<Post>().AnyAsync(p => p.PostId == EligibilityPostId))
        {
            db.Set<Post>().Add(new Post
            {
                PostId = EligibilityPostId,
                RoomId = TestConstants.TestRoomId,
                CharacterId = TestConstants.SecondCharacterId,
                AuthorId = TestConstants.SecondUserId,
                CreatedUtc = DateTimeOffset.UtcNow.AddHours(-11),
                GameText = "Второй игрок отвечает в той же комнате",
                MetagameText = null,
                PrivateAddresseeSnapshotJson = "{}",
                IsRemoved = false
            });
        }

        await db.SaveChangesAsync();
        return previousRating;
    }

    private async Task RevokeEndorsementEligibility(int previousRating)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Set<Post>().Where(p => p.PostId == EligibilityPostId).ExecuteDeleteAsync();
        var author = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
        author.QuantityRating = previousRating;
        await db.SaveChangesAsync();
    }

    private static async Task<(bool CanCreate, string? Reason)> ReadEligibility(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // Enveloped, like every other single-resource read: the answer is under
        // "resource" and not at the root. Asserted by reading it that way rather
        // than by a separate shape test, so a body that loses the envelope fails
        // every assertion this helper feeds.
        var resource = doc.RootElement.GetProperty("resource");
        var canCreate = resource.GetProperty("canCreate").GetBoolean();
        var reason = resource.TryGetProperty("reason", out var r) ? r.GetString() : null;
        return (canCreate, reason);
    }

    /// <summary>
    /// The question the write form asks before it draws itself. A guest is
    /// answered, not refused with 401: "may I?" has an answer for anonymous
    /// readers too, and it is what the form prints instead of the field.
    /// </summary>
    [Fact]
    public async Task GetEligibility_ForGuest_AnswersNoWithSignInSentence()
    {
        var response = await Client.GetAsync(
            $"/v1/users/{TestConstants.TestUserLogin}/endorsements/eligibility");

        var (canCreate, reason) = await ReadEligibility(response);
        canCreate.Should().BeFalse();
        reason.Should().Be("Требуется авторизация");
    }

    [Fact]
    public async Task GetEligibility_ForYourself_AnswersNoWithTheSelfRule()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get,
            $"/v1/users/{TestConstants.TestUserLogin}/endorsements/eligibility");

        var (canCreate, reason) = await ReadEligibility(await Client.SendAsync(request));

        canCreate.Should().BeFalse();
        reason.Should().Be("Нельзя рекомендовать самого себя");
    }

    /// <summary>
    /// The whole path the reader walks: the site asks whether the control may
    /// be drawn, draws it, the POST is accepted, the recommendation is on the
    /// recipient's received page — and asking again now says no, because a
    /// pair may have only one.
    /// </summary>
    [Fact]
    public async Task PostUserEndorsement_ShowsUpInRecipientsListAndClosesThePair()
    {
        var previousRating = await GrantEndorsementEligibility();
        const string text = "Держит темп и не бросает сцену на полуслове";
        Guid? createdId = null;
        var eligibilityUrl = $"/v1/users/{TestConstants.TestUserLogin}/endorsements/eligibility";
        var author = CustomWebApplicationFactory.CreateSecondUser();

        try
        {
            // The right: every rule of the create endpoint is satisfied
            var before = await ReadEligibility(
                await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, eligibilityUrl, author)));
            before.CanCreate.Should().BeTrue();
            before.Reason.Should().BeNull();

            // Writing it
            var post = CreateAuthenticatedRequest(HttpMethod.Post,
                $"/v1/users/{TestConstants.TestUserLogin}/endorsements", author);
            post.Content = JsonContent.Create(new { text });
            var created = await Client.SendAsync(post);
            created.StatusCode.Should().Be(HttpStatusCode.Created);
            using (var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
            {
                createdId = doc.RootElement.GetProperty("id").GetGuid();
            }

            // Where the reader is sent afterwards: the recipient's received page
            var list = await Client.GetAsync($"/v1/users/{TestConstants.TestUserLogin}/endorsements");
            list.StatusCode.Should().Be(HttpStatusCode.OK);
            using (var doc = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
            {
                var row = doc.RootElement.GetProperty("resources").EnumerateArray()
                    .Single(e => e.GetProperty("id").GetGuid() == createdId);
                row.GetProperty("text").GetString().Should().Be(text);
                row.GetProperty("author").GetProperty("username").GetString()
                    .Should().Be(TestConstants.SecondUserLogin);
            }

            // One per pair: the same question now answers no, and the POST
            // agrees with that answer
            var after = await ReadEligibility(
                await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, eligibilityUrl, author)));
            after.CanCreate.Should().BeFalse();
            after.Reason.Should().Be("Вы уже рекомендовали этого пользователя");

            var repeat = CreateAuthenticatedRequest(HttpMethod.Post,
                $"/v1/users/{TestConstants.TestUserLogin}/endorsements", author);
            repeat.Content = JsonContent.Create(new { text = "Вторая рекомендация той же паре" });
            (await Client.SendAsync(repeat)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        finally
        {
            if (createdId.HasValue) await RemoveEndorsements(createdId.Value);
            await RevokeEndorsementEligibility(previousRating);
        }
    }

    [Fact]
    public async Task PostUserEndorsement_RequiresAuthentication()
    {
        // Arrange
        var endorsementData = new
        {
            text = "Great player!"
        };

        // Act - route is /v1/users/{username}/endorsements
        var response = await Client.PostAsJsonAsync($"/v1/users/{TestConstants.SecondUserLogin}/endorsements", endorsementData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
