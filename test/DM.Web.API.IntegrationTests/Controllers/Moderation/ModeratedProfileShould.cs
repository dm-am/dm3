using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbLoginRecord = DM.Infrastructure.Persistence.Entities.Account.UserLoginRecord;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The moderated profile answered 500 for every caller, through three separate
/// defects stacked on one endpoint: two LINQ shapes with no translation (a
/// distinct count inside a group projection, and a constructor call inside
/// another) and an AutoMapper configuration gap on the derived DTO. Nothing exercised the
/// endpoint, so all three shipped.
///
/// The test drives it with login records present — the empty-table path went
/// through the same broken queries but returned before the aggregates could
/// fail, which is how this stayed hidden.
/// </summary>
public class ModeratedProfileShould : IntegrationTestBase
{
    public ModeratedProfileShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task AggregateIpsAndLinkedProfilesForAnAdmin()
    {
        var sharedIp = "203.0.113.77";
        var now = DateTimeOffset.UtcNow;

        await using (var db = DatabaseFixture.CreateDbContext())
        {
            // The subject logs in twice from one address, a second user once
            // from the same address: one IP row, one linked profile.
            db.UserLoginRecords.AddRange(
                new DbLoginRecord
                {
                    UserLoginRecordId = Guid.NewGuid(),
                    UserId = TestConstants.SecondUserId,
                    IpAddress = sharedIp,
                    UserAgent = "e2e",
                    IsSuccessful = true,
                    LoginUtc = now.AddDays(-2)
                },
                new DbLoginRecord
                {
                    UserLoginRecordId = Guid.NewGuid(),
                    UserId = TestConstants.SecondUserId,
                    IpAddress = sharedIp,
                    UserAgent = "e2e",
                    IsSuccessful = true,
                    LoginUtc = now.AddDays(-1)
                },
                new DbLoginRecord
                {
                    UserLoginRecordId = Guid.NewGuid(),
                    UserId = TestConstants.TestUserId,
                    IpAddress = sharedIp,
                    UserAgent = "e2e",
                    IsSuccessful = true,
                    LoginUtc = now.AddHours(-3)
                });
            await db.SaveChangesAsync();
        }

        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/moderation/users/{TestConstants.SecondUserLogin}/profile",
                CustomWebApplicationFactory.CreateAdminUser());

            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = document.RootElement;

            root.GetProperty("username").GetString().Should().Be(TestConstants.SecondUserUsername);

            var ip = root.GetProperty("ipAddresses").EnumerateArray().Single();
            ip.GetProperty("ipAddress").GetString().Should().Be(sharedIp);
            ip.GetProperty("loginsCount").GetInt32().Should().Be(2);

            var linked = root.GetProperty("linkedProfiles").EnumerateArray().Single();
            linked.GetProperty("username").GetString().Should().Be(TestConstants.TestUserUsername);
            linked.GetProperty("sharedIpsCount").GetInt32().Should().Be(1);

            root.GetProperty("loginHistory").EnumerateArray().Should().HaveCount(2);
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.UserLoginRecords.Where(r => r.IpAddress == sharedIp).ExecuteDeleteAsync();
        }
    }
}
