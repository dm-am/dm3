using System.Net;
using System.Text.Json;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;
using DbContestSeries = DM.Infrastructure.Persistence.Entities.Community.ContestSeries;
using DbUserAward = DM.Infrastructure.Persistence.Entities.Community.UserAward;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Everyone awarded in a contest series, with the recipient on the wire for the revoke; revoked grants and other
/// series stay out.
/// </summary>
public class ContestSeriesAwardsShould : IntegrationTestBase
{
    // The series and the award types the listing seeds. xUnit builds the class
    // anew for every test, so the ids are fresh on every run.
    private readonly Guid _seriesId = Guid.NewGuid();
    private readonly Guid _otherSeriesId = Guid.NewGuid();
    private readonly Guid _goldId = Guid.NewGuid();
    private readonly Guid _silverId = Guid.NewGuid();

    public ContestSeriesAwardsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ListRecipientsOfTheSeriesOnly()
    {
        try
        {
            await SeedSeriesAsync();
            await SeedAwardsAsync();

            var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/moderation/contest-series/{_seriesId}/awards",
                CustomWebApplicationFactory.CreateAdminUser());
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var resources = document.RootElement.GetProperty("resources").EnumerateArray().ToList();

            resources.Should().HaveCount(2, "the revoked grant and the other series must not appear");
            resources[0].GetProperty("type").GetProperty("title").GetString().Should().Be("Золото");
            resources[1].GetProperty("type").GetProperty("title").GetString().Should().Be("Серебро");

            // Without the recipient the page cannot revoke: the DELETE route
            // carries the username.
            resources[1].GetProperty("user").GetProperty("username").GetString()
                .Should().Be(TestConstants.SecondUserLogin);
        }
        finally
        {
            await DropSeriesAsync();
        }
    }

    [Fact]
    public async Task RejectAnUnknownSeries()
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/v1/moderation/contest-series/{Guid.NewGuid()}/awards",
            CustomWebApplicationFactory.CreateAdminUser());

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task BeClosedToRegularUsers()
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/v1/moderation/contest-series/{Guid.NewGuid()}/awards");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Two series of one contest type and the two award types they share.
    /// </summary>
    private async Task SeedSeriesAsync()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.ContestSeries.AddRange(
            new DbContestSeries
            {
                ContestSeriesId = _seriesId,
                ContestType = ContestType.Literary,
                Number = 900,
                Year = 2024,
                IsActive = true,
            },
            new DbContestSeries
            {
                ContestSeriesId = _otherSeriesId,
                ContestType = ContestType.Literary,
                Number = 901,
                Year = 2024,
                IsActive = true,
            });

        // Two types with different SortOrder: the listing is a podium, so
        // silver must follow gold regardless of when it was granted.
        db.AwardTypes.AddRange(
            new DbAwardType
            {
                AwardTypeId = _goldId,
                Code = "test_gold_900",
                Title = "Золото",
                Description = "Первое место",
                IconName = "trophy-cup",
                Tier = 1,
                SortOrder = 1,
                IsActive = true,
            },
            new DbAwardType
            {
                AwardTypeId = _silverId,
                Code = "test_silver_900",
                Title = "Серебро",
                Description = "Второе место",
                IconName = "trophy-cup",
                Tier = 2,
                SortOrder = 2,
                IsActive = true,
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// The awards: a silver and a gold granted in the series, a gold revoked in
    /// it, and a gold granted in the other series.
    /// </summary>
    private async Task SeedAwardsAsync()
    {
        var grantedId = Guid.NewGuid();
        var revokedId = Guid.NewGuid();
        var otherSeriesGrantId = Guid.NewGuid();

        await using var db = DatabaseFixture.CreateDbContext();
        db.UserAwards.AddRange(
            new DbUserAward
            {
                UserAwardId = grantedId,
                UserId = TestConstants.SecondUserId,
                AwardTypeId = _silverId,
                ContestSeriesId = _seriesId,
                AwardedUtc = DateTimeOffset.UtcNow.AddDays(-3),
                AwardedByUserId = TestConstants.SeniorModeratorUserId,
            },
            new DbUserAward
            {
                UserAwardId = Guid.NewGuid(),
                UserId = TestConstants.TestUserId,
                AwardTypeId = _goldId,
                ContestSeriesId = _seriesId,
                AwardedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                AwardedByUserId = TestConstants.SeniorModeratorUserId,
            },
            new DbUserAward
            {
                UserAwardId = revokedId,
                UserId = TestConstants.ModeratorUserId,
                AwardTypeId = _goldId,
                ContestSeriesId = _seriesId,
                AwardedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                AwardedByUserId = TestConstants.SeniorModeratorUserId,
                IsRemoved = true,
            },
            new DbUserAward
            {
                UserAwardId = otherSeriesGrantId,
                UserId = TestConstants.MentorUserId,
                AwardTypeId = _goldId,
                ContestSeriesId = _otherSeriesId,
                AwardedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                AwardedByUserId = TestConstants.SeniorModeratorUserId,
            });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Drops every row the two seeds wrote, the revoked award included.
    /// </summary>
    private async Task DropSeriesAsync()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        // IgnoreQueryFilters: the revoked grant is invisible to the
        // IsRemoved filter, and leaving it behind breaks the FK below.
        await db.UserAwards
            .IgnoreQueryFilters()
            .Where(a => a.ContestSeriesId == _seriesId || a.ContestSeriesId == _otherSeriesId)
            .ExecuteDeleteAsync();
        await db.AwardTypes
            .Where(t => t.AwardTypeId == _goldId || t.AwardTypeId == _silverId)
            .ExecuteDeleteAsync();
        await db.ContestSeries
            .Where(s => s.ContestSeriesId == _seriesId || s.ContestSeriesId == _otherSeriesId)
            .ExecuteDeleteAsync();
    }
}
