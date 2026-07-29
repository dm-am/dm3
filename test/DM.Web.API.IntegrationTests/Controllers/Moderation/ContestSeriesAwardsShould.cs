using System;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbAwardType = DM.Infrastructure.Persistence.Entities.Community.AwardType;
using DbContestSeries = DM.Infrastructure.Persistence.Entities.Community.ContestSeries;
using DbUserAward = DM.Infrastructure.Persistence.Entities.Community.UserAward;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The contest series admin page promised a list of everyone awarded in the
/// series and a revoke action, but no endpoint could answer "who holds awards
/// in this series" — the page kept a session accumulator that never
/// accumulated, over a model with no recipient. These tests pin the endpoint
/// that replaced it: the recipient must be on the wire (revoking needs it),
/// revoked grants must be gone, and other series must not leak in.
/// </summary>
public class ContestSeriesAwardsShould : IntegrationTestBase
{
    public ContestSeriesAwardsShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ListRecipientsOfTheSeriesOnly()
    {
        var seriesId = Guid.NewGuid();
        var otherSeriesId = Guid.NewGuid();
        var goldId = Guid.NewGuid();
        var silverId = Guid.NewGuid();
        var grantedId = Guid.NewGuid();
        var revokedId = Guid.NewGuid();
        var otherSeriesGrantId = Guid.NewGuid();

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.ContestSeries.AddRange(
                new DbContestSeries
                {
                    ContestSeriesId = seriesId,
                    ContestType = ContestType.Literary,
                    Number = 900,
                    Year = 2024,
                    IsActive = true,
                },
                new DbContestSeries
                {
                    ContestSeriesId = otherSeriesId,
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
                    AwardTypeId = goldId,
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
                    AwardTypeId = silverId,
                    Code = "test_silver_900",
                    Title = "Серебро",
                    Description = "Второе место",
                    IconName = "trophy-cup",
                    Tier = 2,
                    SortOrder = 2,
                    IsActive = true,
                });

            db.UserAwards.AddRange(
                new DbUserAward
                {
                    UserAwardId = grantedId,
                    UserId = TestConstants.SecondUserId,
                    AwardTypeId = silverId,
                    ContestSeriesId = seriesId,
                    AwardedUtc = DateTimeOffset.UtcNow.AddDays(-3),
                    AwardedByUserId = TestConstants.SeniorModeratorUserId,
                },
                new DbUserAward
                {
                    UserAwardId = Guid.NewGuid(),
                    UserId = TestConstants.TestUserId,
                    AwardTypeId = goldId,
                    ContestSeriesId = seriesId,
                    AwardedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    AwardedByUserId = TestConstants.SeniorModeratorUserId,
                },
                new DbUserAward
                {
                    UserAwardId = revokedId,
                    UserId = TestConstants.ModeratorUserId,
                    AwardTypeId = goldId,
                    ContestSeriesId = seriesId,
                    AwardedUtc = DateTimeOffset.UtcNow.AddDays(-2),
                    AwardedByUserId = TestConstants.SeniorModeratorUserId,
                    IsRemoved = true,
                },
                new DbUserAward
                {
                    UserAwardId = otherSeriesGrantId,
                    UserId = TestConstants.MentorUserId,
                    AwardTypeId = goldId,
                    ContestSeriesId = otherSeriesId,
                    AwardedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                    AwardedByUserId = TestConstants.SeniorModeratorUserId,
                });

            await db.SaveChangesAsync();
        }

        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/moderation/contest-series/{seriesId}/awards",
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
                .Should().Be(TestConstants.SecondUserUsername);
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            // IgnoreQueryFilters: the revoked grant is invisible to the
            // IsRemoved filter, and leaving it behind breaks the FK below.
            await db.UserAwards
                .IgnoreQueryFilters()
                .Where(a => a.ContestSeriesId == seriesId || a.ContestSeriesId == otherSeriesId)
                .ExecuteDeleteAsync();
            await db.AwardTypes
                .Where(t => t.AwardTypeId == goldId || t.AwardTypeId == silverId)
                .ExecuteDeleteAsync();
            await db.ContestSeries
                .Where(s => s.ContestSeriesId == seriesId || s.ContestSeriesId == otherSeriesId)
                .ExecuteDeleteAsync();
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
}
