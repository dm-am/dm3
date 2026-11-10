using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbBan = DM.Infrastructure.Persistence.Entities.Moderation.Ban;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class BanControllerShould : IntegrationTestBase
{
    public BanControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // Seed an active temporary ban for SecondUser directly: the public GETs run anonymously.
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
    public async Task GetUserBansPublicViewReturnsBanFactsWithoutModerationDetails()
    {
        var banId = await SeedActiveBan();

        try
        {
            // Anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/bans");

            // Ban facts are present, moderation details are not
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
    public async Task GetActiveBanPublicViewReturnsBanFactsWithoutModerationDetails()
    {
        var banId = await SeedActiveBan();

        try
        {
            // Anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/bans/active");

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
    public async Task GetBansRequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/bans");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The ban list is public, as the warning log and the pulse are: a guest reads it, with the
    // reason and the moderator, and is offered no lift.
    [Fact]
    public async Task LetAGuestReadTheBanList()
    {
        var banId = await SeedActiveBan();

        try
        {
            var response = await Client.GetAsync("/v1/bans/history?skip=0&take=200");
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

            using var document = JsonDocument.Parse(body);
            var ban = document.RootElement.GetProperty("resources").EnumerateArray()
                .Select(entry => entry.GetProperty("ban"))
                .First(entry => entry.GetProperty("id").GetGuid() == banId);
            ban.GetProperty("canLift").GetBoolean().Should().BeFalse();
            ban.TryGetProperty("moderator", out _).Should().BeTrue();
            ban.TryGetProperty("comment", out _).Should().BeTrue();
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    [Fact]
    public async Task GetBansWithAuthRequiresModeratorRole()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/bans");

        var response = await Client.SendAsync(request);

        // Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // The lift is a decision of its own in the list; the reason travels in the body of the DELETE.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefuseToLiftABanWithoutAReason(bool sendBlankReason)
    {
        var banId = await SeedActiveBan();

        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/bans/{banId}",
                CustomWebApplicationFactory.CreateSeniorModeratorUser());
            request.Content = sendBlankReason
                ? JsonContent.Create(new { reason = "  " })
                : JsonContent.Create(new { });

            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await using var db = DatabaseFixture.CreateDbContext();
            (await db.Bans.SingleAsync(b => b.BanId == banId)).LiftedUtc.Should().BeNull(
                "a refused lift leaves the ban running");
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    [Fact]
    public async Task LiftABanWithItsReasonAndSignItInTheHistory()
    {
        var banId = await SeedActiveBan();

        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/bans/{banId}",
                CustomWebApplicationFactory.CreateSeniorModeratorUser());
            request.Content = JsonContent.Create(new { reason = "Разобрались, нарушения не было" });

            var response = await Client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var entries = Entries(await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                $"username={TestConstants.SecondUserLogin}&skip=0&take=100"));

            entries.Select(e => (Kind(e), BanId(e))).Should().Equal(
                [("Lifted", banId), ("Issued", banId)],
                "the lift happened after the issue, and both stay in the list");
            var lifted = entries[0].GetProperty("ban");
            lifted.GetProperty("liftReason").GetString().Should().Be("Разобрались, нарушения не было");
            lifted.GetProperty("liftedBy").GetProperty("username").GetString().Should().Be(
                TestConstants.SeniorModeratorUserLogin,
                "the list signs the lift with a link to whoever lifted it, as it signs the issue");
            lifted.TryGetProperty("liftedByUserId", out _).Should().BeFalse(
                "the list signs the lift with the user, the bare identifier is not what it prints");
            entries[0].GetProperty("occurredUtc").GetDateTimeOffset().Should().Be(
                lifted.GetProperty("liftedUtc").GetDateTimeOffset(), "a lift stands at the moment it was made");
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    // The second ban is issued between the issue and the lift of the first; somebody else's stays
    // out.
    [Fact]
    public async Task ListEveryIssueAndEveryLiftAsAnEntryOfItsOwnNewestFirst()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var first = await SeedBan(TestConstants.MentorUserId, now.AddDays(-3), now.AddDays(4),
            AccessPolicy.DemocraticBan, liftedUtc: now.AddDays(-1));
        var second = await SeedBan(TestConstants.MentorUserId, now.AddDays(-2), now.AddDays(5),
            AccessPolicy.FullBan);
        var stranger = await SeedBan(TestConstants.SecondUserId, now.AddDays(-2).AddHours(6), now.AddDays(5),
            AccessPolicy.DemocraticBan);

        try
        {
            var root = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                $"username={TestConstants.MentorUserLogin}&skip=0&take=20");
            var entries = Entries(root);

            entries.Select(e => (Kind(e), BanId(e), e.GetProperty("occurredUtc").GetDateTimeOffset()))
                .Should().Equal(
                    ("Lifted", first, now.AddDays(-1)),
                    ("Issued", second, now.AddDays(-2)),
                    ("Issued", first, now.AddDays(-3)));
            root.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(3,
                "the page counts entries: two bans, one of them lifted, are three of them");

            var everybody = Entries(await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                "skip=0&take=200"));
            everybody.Should().Contain(e => BanId(e) == stranger,
                "without a name the list is the whole website's");
        }
        finally
        {
            await RemoveBans(first, second, stranger);
        }
    }

    // Read from the database the stand runs, which cuts the page.
    [Fact]
    public async Task NumberEveryEntryFromTheOldestAndKeepTheNumbersWhenANewOneIsWritten()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var lifted = await SeedBan(TestConstants.MentorUserId, now.AddDays(-3), now.AddDays(4),
            AccessPolicy.DemocraticBan, liftedUtc: now.AddDays(-1));
        var running = await SeedBan(TestConstants.MentorUserId, now.AddDays(-2), now.AddDays(5),
            AccessPolicy.FullBan);
        Guid? newest = null;

        try
        {
            var root = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(), "skip=0&take=200");
            var total = root.GetProperty("paging").GetProperty("total").GetInt32();
            var numbers = Entries(root).Select(Number).ToList();
            numbers.Should().Equal(Enumerable.Range(total - numbers.Count + 1, numbers.Count).Reverse(),
                "every entry of the list has a number of its own, and the newest has the greatest");

            var before = NumbersOf(root, lifted, running);

            newest = await SeedBan(TestConstants.SecondUserId, now, now.AddDays(7), AccessPolicy.DemocraticBan);
            var after = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(), "skip=0&take=200");

            NumbersOf(after, lifted, running).Should().Equal(before,
                "a new entry moves the others down the list and leaves their numbers");
            Number(Entries(after).Single(e => BanId(e) == newest)).Should().Be(total + 1);
        }
        finally
        {
            await RemoveBans(newest is { } id ? new[] { lifted, running, id } : new[] { lifted, running });
        }
    }

    [Fact]
    public async Task OpenThePageOfAnEntryByItsNumber()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var older = await SeedBan(TestConstants.MentorUserId, now.AddDays(-3), now.AddDays(4),
            AccessPolicy.DemocraticBan, liftedUtc: now.AddDays(-1));
        var stranger = await SeedBan(TestConstants.SecondUserId, now.AddDays(-2).AddHours(6), now.AddDays(5),
            AccessPolicy.DemocraticBan);
        var newer = await SeedBan(TestConstants.MentorUserId, now.AddDays(-2), now.AddDays(5),
            AccessPolicy.FullBan);

        try
        {
            var everybody = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(), "skip=0&take=200");
            var his = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                $"username={TestConstants.MentorUserLogin}&skip=0&take=200");
            NumbersOf(his, older, newer).Should().Equal(NumbersOf(everybody, older, newer),
                "an entry has one number, the whole list's");

            // His list, newest first: the lift of the older ban, the newer ban,
            // the older ban. The older ban's issue is the third entry, on the
            // third page of one.
            var hisEntries = Entries(his);
            var position = hisEntries.FindIndex(e => BanId(e) == older && Kind(e) == "Issued");
            position.Should().Be(2);
            var issue = Number(hisEntries[position]);
            var page = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                $"username={TestConstants.MentorUserLogin}&skip=0&take=1&entry={issue}");

            Entries(page).Select(e => (Kind(e), BanId(e), Number(e))).Should().Equal(("Issued", older, issue));
            page.GetProperty("paging").GetProperty("skip").GetInt32().Should().Be(2);

            var strangers = Number(Entries(everybody).Single(e => BanId(e) == stranger));
            var notHis = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                $"username={TestConstants.MentorUserLogin}&skip=1&take=1&entry={strangers}");
            notHis.GetProperty("paging").GetProperty("skip").GetInt32().Should().Be(1,
                "an entry of somebody else leaves the page to the offset asked for");
        }
        finally
        {
            await RemoveBans(older, stranger, newer);
        }
    }

    // A name nobody carries narrows the list to nothing, the way the warning log answers it, rather
    // than being ignored or refused.
    [Fact]
    public async Task AnswerANameNobodyCarriesWithAnEmptyPage()
    {
        var banId = await SeedActiveBan();

        try
        {
            var root = await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                "username=nobody-carries-this-name&skip=0&take=20");

            Entries(root).Should().BeEmpty();
            root.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(0);
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    // A senior moderator lifts a running temporary ban of somebody else's and nothing more; a
    // moderator nothing.
    [Fact]
    public async Task TellTheScopeOfEachBanAndWhetherTheReaderMayLiftIt()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var running = await SeedBan(TestConstants.MentorUserId, now.AddDays(-1), now.AddDays(6),
            AccessPolicy.FullBan);
        var permanent = await SeedBan(TestConstants.SecondUserId, now.AddDays(-1), now.AddYears(100),
            AccessPolicy.DemocraticBan);
        var own = await SeedBan(TestConstants.SeniorModeratorUserId, now.AddDays(-1), now.AddDays(6),
            AccessPolicy.DemocraticBan);
        var expired = await SeedBan(TestConstants.TestUserId, now.AddDays(-9), now.AddDays(-2),
            AccessPolicy.DemocraticBan);

        try
        {
            var senior = IssuedBans(await ReadHistory(
                CustomWebApplicationFactory.CreateSeniorModeratorUser(), "skip=0&take=200"));
            var admin = IssuedBans(await ReadHistory(
                CustomWebApplicationFactory.CreateAdminUser(), "skip=0&take=200"));
            var moderator = IssuedBans(await ReadHistory(
                CustomWebApplicationFactory.CreateModeratorUser(), "skip=0&take=200"));

            senior[running].GetProperty("accessPolicy").GetString().Should().Be("FullBan");
            senior[permanent].GetProperty("accessPolicy").GetString().Should().Be("DemocraticBan");

            CanLift(senior, running).Should().BeTrue("a running temporary ban of somebody else's");
            CanLift(senior, permanent).Should().BeFalse("a permanent ban is an administrator's to lift");
            CanLift(senior, own).Should().BeFalse("nobody lifts his own ban");
            CanLift(senior, expired).Should().BeFalse("a ban that has run out has nothing left to lift");
            CanLift(admin, permanent).Should().BeTrue();
            CanLift(admin, own).Should().BeTrue("the ban is the senior moderator's own, not the administrator's");
            new[] { running, permanent, own, expired }.Should().OnlyContain(id => !CanLift(moderator, id),
                "an ordinary moderator lifts no ban");
        }
        finally
        {
            await RemoveBans(running, permanent, own, expired);
        }
    }

    // The reason is written in the BBCode editor, so the list shows it rendered.
    [Fact]
    public async Task RenderTheReasonOfABan()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var banId = await SeedBan(TestConstants.MentorUserId, now.AddDays(-1), now.AddDays(6),
            AccessPolicy.DemocraticBan, comment: "[b]Спам[/b] в глобальном чате");

        try
        {
            var comment = IssuedBans(await ReadHistory(CustomWebApplicationFactory.CreateModeratorUser(),
                    $"username={TestConstants.MentorUserLogin}&skip=0&take=20"))[banId]
                .GetProperty("comment").GetString();

            comment.Should().Contain("<strong>Спам</strong>").And.NotContain("[b]");
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    // A ban that has run out is not lifted: nothing is left of it to end.
    [Fact]
    public async Task RefuseToLiftABanThatHasRunOut()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var banId = await SeedBan(TestConstants.MentorUserId, now.AddDays(-9), now.AddDays(-2),
            AccessPolicy.DemocraticBan);

        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Delete, $"/v1/bans/{banId}",
                CustomWebApplicationFactory.CreateSeniorModeratorUser());
            request.Content = JsonContent.Create(new { reason = "Разобрались" });

            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            await using var db = DatabaseFixture.CreateDbContext();
            (await db.Bans.SingleAsync(b => b.BanId == banId)).LiftedUtc.Should().BeNull();
        }
        finally
        {
            await RemoveBan(banId);
        }
    }

    /// <summary>
    /// A ban written straight into the table, by the senior moderator unless the
    /// test says otherwise.
    /// </summary>
    private async Task<Guid> SeedBan(Guid targetUserId, DateTimeOffset startedUtc, DateTimeOffset endedUtc,
        AccessPolicy accessPolicy, string comment = "Нарушение правил глобального чата",
        DateTimeOffset? liftedUtc = null)
    {
        var banId = Guid.NewGuid();
        await using var db = DatabaseFixture.CreateDbContext();
        db.Bans.Add(new DbBan
        {
            BanId = banId,
            TargetUserId = targetUserId,
            AuthorId = TestConstants.SeniorModeratorUserId,
            StartedUtc = startedUtc,
            EndedUtc = endedUtc,
            Comment = comment,
            AccessRestrictionPolicy = accessPolicy,
            IsVoluntary = false,
            LiftedUtc = liftedUtc,
            LiftedByUserId = liftedUtc.HasValue ? TestConstants.AdminUserId : null,
            LiftReason = liftedUtc.HasValue ? "Разобрались" : null,
        });
        await db.SaveChangesAsync();
        return banId;
    }

    private async Task RemoveBans(params Guid[] banIds)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Bans.Where(b => banIds.Contains(b.BanId)).ExecuteDeleteAsync();
    }

    // The moment to the whole second: the column keeps microseconds and the clock ticks, and an
    // entry is compared with the moment it was written at.
    private static DateTimeOffset WholeSeconds(DateTimeOffset moment) =>
        new(moment.Ticks - moment.Ticks % TimeSpan.TicksPerSecond, moment.Offset);

    private async Task<JsonElement> ReadHistory(GeneralUser reader, string query)
    {
        var response = await Client.SendAsync(
            CreateAuthenticatedRequest(HttpMethod.Get, $"/v1/bans/history?{query}", reader));
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static List<JsonElement> Entries(JsonElement root) =>
        root.GetProperty("resources").EnumerateArray().ToList();

    private static string? Kind(JsonElement entry) =>
        entry.TryGetProperty("kind", out var kind) ? kind.GetString() : null;

    private static Guid? BanId(JsonElement entry) =>
        entry.TryGetProperty("ban", out var ban) ? ban.GetProperty("id").GetGuid() : null;

    private static int Number(JsonElement entry) => entry.GetProperty("number").GetInt32();

    private static List<int> NumbersOf(JsonElement root, params Guid[] banIds) => Entries(root)
        .Where(e => BanId(e) is { } id && banIds.Contains(id))
        .Select(Number)
        .ToList();

    private static Dictionary<Guid, JsonElement> IssuedBans(JsonElement root) => Entries(root)
        .Where(e => Kind(e) == "Issued")
        .ToDictionary(e => BanId(e)!.Value, e => e.GetProperty("ban"));

    private static bool CanLift(Dictionary<Guid, JsonElement> bans, Guid banId) =>
        bans[banId].GetProperty("canLift").GetBoolean();
}
