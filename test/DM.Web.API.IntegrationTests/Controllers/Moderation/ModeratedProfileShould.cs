using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbLoginRecord = DM.Infrastructure.Persistence.Entities.Account.UserLoginRecord;
using DbModeratedProfileNote = DM.Infrastructure.Persistence.Entities.Account.ModeratedProfileNote;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

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

    /// <summary>
    /// The buttons of the profile offer a warning and a ban by the rank rule the
    /// two services refuse by (ModerationRank): only to a rank below the
    /// caller's, never to an administrator. A moderator saw "Выдать
    /// предупреждение" on the profile of a senior moderator and of an
    /// administrator, and a senior moderator "Выдать бан" on an
    /// administrator's, each a write the server then refused.
    /// </summary>
    [Theory]
    [InlineData("moderator", TestConstants.TestUserLogin, true, false)]
    [InlineData("moderator", TestConstants.ModeratorUserLogin, false, false)]
    [InlineData("moderator", TestConstants.SeniorModeratorUserLogin, false, false)]
    [InlineData("moderator", TestConstants.AdminUserLogin, false, false)]
    [InlineData("senior", TestConstants.ModeratorUserLogin, true, true)]
    [InlineData("senior", TestConstants.SeniorModeratorUserLogin, false, false)]
    [InlineData("senior", TestConstants.AdminUserLogin, false, false)]
    [InlineData("admin", TestConstants.SeniorModeratorUserLogin, true, true)]
    [InlineData("admin", TestConstants.AdminUserLogin, false, false)]
    public async Task OfferAWarningAndABanOnlyBelowTheCallersRank(
        string caller, string target, bool canWarn, bool canBan)
    {
        var identity = caller switch
        {
            "moderator" => CustomWebApplicationFactory.CreateModeratorUser(),
            "senior" => CustomWebApplicationFactory.CreateSeniorModeratorUser(),
            _ => CustomWebApplicationFactory.CreateAdminUser()
        };

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/moderation/users/{target}/profile", identity));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var permissions = document.RootElement.GetProperty("permissions");
        permissions.GetProperty("canIssueWarning").GetBoolean().Should().Be(canWarn);
        permissions.GetProperty("canIssueBan").GetBoolean().Should().Be(canBan);
    }

    /// <summary>
    /// What each rank is shown of a profile and offered on it, flag by flag.
    /// </summary>
    /// <remarks>
    /// The flags are decided by rank thresholds, and the same thresholds decide
    /// what the moderation services refuse, so the answer is held here whichever
    /// layer computes it. Every flag that is on for a rank is listed; every other
    /// one is off. The subject is an ordinary member, below every caller,
    /// with a note from each of the three callers, so a note is either the
    /// caller's own or another moderator's.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.Moderator, new[]
    {
        "canCreateModNote", "canIssueWarning", "canSetModerationWatch", "canViewLinkedProfiles",
        "canViewModNotes", "deletes own note", "edits own note",
    })]
    [InlineData(UserRole.SeniorModerator, new[]
    {
        "canCreateModNote", "canIssueBan", "canIssueWarning", "canLiftBan", "canSetModerationWatch",
        "canViewLinkedProfiles", "canViewModNotes", "deletes others' notes", "deletes own note",
        "edits others' notes", "edits own note",
    })]
    [InlineData(UserRole.Admin, new[]
    {
        "canCreateModNote", "canIssueBan", "canIssueWarning", "canLiftBan", "canSetModerationWatch",
        "canViewEmail", "canViewIpAddresses", "canViewLinkedProfiles", "canViewLoginHistory",
        "canViewModNotes", "deletes others' notes", "deletes own note", "edits others' notes",
        "edits own note", "shows email", "shows ip addresses", "shows login history",
    })]
    public async Task ShowAndOfferEachRankWhatItsRankAllows(UserRole rank, string[] granted)
    {
        var caller = rank switch
        {
            UserRole.Moderator => CustomWebApplicationFactory.CreateModeratorUser(),
            UserRole.SeniorModerator => CustomWebApplicationFactory.CreateSeniorModeratorUser(),
            _ => CustomWebApplicationFactory.CreateAdminUser()
        };
        var subject = await SeedSubjectWithNotesAsync();

        var response = await Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/moderation/users/{subject}/profile", caller));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Granted(document.RootElement, caller.UserId).Should().Equal(granted,
            $"this is what a {rank} is shown and offered on the profile of a member below every rank");
    }

    /// <summary>The ranks below a moderator are not let in, and a guest is asked to sign in.</summary>
    [Theory]
    [InlineData(UserRole.Mentor, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.RegularUser, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Guest, HttpStatusCode.Unauthorized)]
    public async Task RefuseEveryRankBelowAModerator(UserRole rank, HttpStatusCode expected)
    {
        var path = $"/v1/moderation/users/{TestConstants.SecondUserLogin}/profile";
        using var request = rank == UserRole.Guest
            ? new HttpRequestMessage(HttpMethod.Get, path)
            : CreateAuthenticatedRequest(HttpMethod.Get, path, new GeneralUser
            {
                UserId = rank == UserRole.Mentor ? TestConstants.MentorUserId : TestConstants.TestUserId,
                Username = rank == UserRole.Mentor ? TestConstants.MentorUserLogin : TestConstants.TestUserLogin,
                Role = rank,
            });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(expected);
    }

    /// <summary>
    /// The flags that are on, the fields that are filled, and what the caller may
    /// do to the notes, as one sorted list.
    /// </summary>
    private static IReadOnlyList<string> Granted(JsonElement profile, Guid callerId)
    {
        var granted = profile.GetProperty("permissions").EnumerateObject()
            .Where(flag => flag.Value.ValueKind == JsonValueKind.True)
            .Select(flag => flag.Name)
            .ToList();

        if (profile.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
        {
            granted.Add("shows email");
        }

        if (profile.TryGetProperty("ipAddresses", out var ips) && ips.ValueKind == JsonValueKind.Array)
        {
            granted.Add("shows ip addresses");
        }

        if (profile.TryGetProperty("loginHistory", out var history) && history.ValueKind == JsonValueKind.Array)
        {
            granted.Add("shows login history");
        }

        var notes = profile.GetProperty("moderatorNotes").EnumerateArray().ToList();
        notes.Should().HaveCount(3, "each of the three callers left a note on the subject");

        var own = notes.Where(note => note.GetProperty("authorId").GetGuid() == callerId).ToList();
        var others = notes.Except(own).ToList();
        own.Should().ContainSingle();

        if (own.All(note => note.GetProperty("canEdit").GetBoolean())) granted.Add("edits own note");
        if (own.All(note => note.GetProperty("canDelete").GetBoolean())) granted.Add("deletes own note");
        if (others.All(note => note.GetProperty("canEdit").GetBoolean())) granted.Add("edits others' notes");
        if (others.All(note => note.GetProperty("canDelete").GetBoolean())) granted.Add("deletes others' notes");

        return granted.OrderBy(name => name, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// A member of no rank with an address and a note from each of the three
    /// callers. Made per test: notes on a shared user would be counted by every
    /// test that reads that user's profile.
    /// </summary>
    /// <returns>The member's username.</returns>
    private async Task<string> SeedSubjectWithNotesAsync()
    {
        await using var db = DatabaseFixture.CreateDbContext();

        var subjectId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed.
        var username = $"subj{subjectId:N}"[..20];
        db.Users.Add(new DbUser
        {
            UserId = subjectId,
            Username = username,
            Email = $"{subjectId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });

        foreach (var authorId in new[]
                 {
                     TestConstants.ModeratorUserId, TestConstants.SeniorModeratorUserId, TestConstants.AdminUserId,
                 })
        {
            db.ModeratedProfileNotes.Add(new DbModeratedProfileNote
            {
                ModeratedProfileNoteId = Guid.NewGuid(),
                UserId = subjectId,
                AuthorId = authorId,
                Text = "Заметка",
                CreatedUtc = DateTimeOffset.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return username;
    }
}
