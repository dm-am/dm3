using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for BanController
/// </summary>
public class BanControllerShould : IntegrationTestBase
{
    public BanControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    /// <summary>
    /// Seed an active temporary ban for SecondUser directly:
    /// the create endpoint requires a moderator identity, while
    /// the public GETs must be exercised anonymously.
    /// </summary>
    private async Task<Guid> SeedActiveBan()
    {
        var banId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Bans.Add(new DbBan
        {
            BanId = banId,
            TargetUserId = TestConstants.SecondUserId,
            AuthorId = TestConstants.ModeratorUserId,
            StartedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            EndedUtc = DateTimeOffset.UtcNow.AddDays(1),
            Comment = "Секретный комментарий модератора",
            AccessRestrictionPolicy = AccessPolicy.FullBan,
            IsVoluntary = false,
        });
        await db.SaveChangesAsync();
        return banId;
    }

    private async Task RemoveBan(Guid banId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Bans.Where(b => b.BanId == banId).ExecuteDeleteAsync();
    }

    private static void AssertPublicBanShape(JsonElement ban)
    {
        ban.GetProperty("type").GetString().Should().Be("Temporary");
        ban.GetProperty("isActive").GetBoolean().Should().BeTrue();
        ban.TryGetProperty("startedUtc", out _).Should().BeTrue();
        ban.TryGetProperty("expiresUtc", out _).Should().BeTrue();
        ban.TryGetProperty("comment", out _).Should().BeFalse();
        ban.TryGetProperty("moderator", out _).Should().BeFalse();
        ban.TryGetProperty("user", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetUserBans_PublicView_ReturnsBanFactsWithoutModerationDetails()
    {
        // Arrange
        var banId = await SeedActiveBan();

        try
        {
            // Act - anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/bans");

            // Assert - ban facts are present, moderation details are not
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("isBanned").GetBoolean().Should().BeTrue();

            AssertPublicBanShape(root.GetProperty("activeBan"));

            var history = root.GetProperty("history").EnumerateArray().ToList();
            history.Should().HaveCount(1);
            AssertPublicBanShape(history[0]);

            json.Should().NotContain("Секретный комментарий модератора");
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    [Fact]
    public async Task GetActiveBan_PublicView_ReturnsBanFactsWithoutModerationDetails()
    {
        // Arrange
        var banId = await SeedActiveBan();

        try
        {
            // Act - anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/bans/active");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            AssertPublicBanShape(doc.RootElement.GetProperty("resource"));
            json.Should().NotContain("Секретный комментарий модератора");
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    [Fact]
    public async Task GetBans_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/bans");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBans_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/bans");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostBan_RequiresAuthentication()
    {
        // Arrange
        var banData = new
        {
            userLogin = TestConstants.SecondUserLogin,
            reason = "Violation",
            duration = "P7D"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/bans", banData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task DeleteBan_RequiresAuthentication()
    {
        // Arrange
        var banId = Guid.NewGuid();

        // Act
        var response = await Client.DeleteAsync($"/v1/bans/{banId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
