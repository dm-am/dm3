using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
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
