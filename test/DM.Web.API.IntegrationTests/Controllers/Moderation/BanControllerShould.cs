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

    /// <summary>
    /// A lift without a reason is refused and leaves the ban in force.
    /// </summary>
    /// <remarks>
    /// The ban list draws the lift as a decision of its own under the ban, and
    /// one without words would be the only decision there nobody explained. The
    /// reason travels in the body of the DELETE, as the removal of an account
    /// sends its own.
    /// </remarks>
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

    /// <summary>
    /// A lift with its reason goes through, and the history signs it by name: the
    /// lift is an entry of its own, and the issue it ended stays where it was.
    /// </summary>
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

    /// <summary>
    /// The ban list is the bot's ban log of DM2: every issue and every lift is an
    /// entry of its own, at the moment it happened, newest first, and the page
    /// counts entries rather than bans.
    /// </summary>
    /// <remarks>
    /// The second ban is issued between the issue and the lift of the first, so a
    /// list of bans by their start, which is what the history used to be, puts the
    /// first ban's lift in the wrong place: under the second ban instead of above
    /// it. The name narrows the list to one person, and somebody else's ban issued
    /// in the middle of it all stays out.
    /// </remarks>
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

    /// <summary>
    /// Every entry carries its number in the whole list, counted from the oldest:
    /// the list, newest first, counts down by one from the count of entries to 1,
    /// and a new entry takes the next number and leaves the others theirs.
    /// </summary>
    /// <remarks>
    /// Read from the database the stand runs, because the numbers are counted by
    /// the order the database cuts the page by.
    /// </remarks>
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

    /// <summary>
    /// The list of one person shows the numbers the whole list gives his entries,
    /// and the page of an entry is asked for by its number: the answer is the page
    /// of this list that holds it, and says which page that is.
    /// </summary>
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

    /// <summary>
    /// A name nobody carries narrows the list to nothing, the way the warning log
    /// answers it, rather than being ignored or refused.
    /// </summary>
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

    /// <summary>
    /// Each ban says what it forbids and whether the reader may lift it, so the
    /// list names the scope in words and offers the lift only where the lift
    /// would go through.
    /// </summary>
    /// <remarks>
    /// The flag answers for the reader. A senior moderator may lift a running
    /// temporary ban of somebody else's and nothing more: not a permanent one,
    /// which is an administrator's, not his own, not one that has run out or
    /// been lifted. An ordinary moderator may lift nothing.
    /// </remarks>
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

    /// <summary>
    /// The window that issues a ban takes its reason in the BBCode editor, so the
    /// list shows the reason rendered, the way a comment is, and not with its
    /// brackets.
    /// </summary>
    [Fact]
    public async Task RenderTheReasonOfABan()
    {
        var now = WholeSeconds(DateTimeOffset.UtcNow);
        var banId = await SeedBan(TestConstants.MentorUserId, now.AddDays(-1), now.AddDays(6),
            AccessPolicy.DemocraticBan, comment: "[b]Спам[/b] в общем чате");

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

    /// <summary>
    /// A ban that has run out is not lifted: there is nothing left of it to end,
    /// and a lift written after its end would stand in the list as an entry about
    /// a ban that was already over.
    /// </summary>
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
        AccessPolicy accessPolicy, string comment = "Нарушение правил общего чата",
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

    /// <summary>
    /// The moment to the whole second: the column keeps microseconds and the
    /// clock ticks, and an entry is compared with the moment it was written at.
    /// </summary>
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

    /// <summary>The numbers of the entries of the given bans, in the order of the page.</summary>
    private static List<int> NumbersOf(JsonElement root, params Guid[] banIds) => Entries(root)
        .Where(e => BanId(e) is { } id && banIds.Contains(id))
        .Select(Number)
        .ToList();

    /// <summary>The bans of the page, each read off the entry that issued it.</summary>
    private static Dictionary<Guid, JsonElement> IssuedBans(JsonElement root) => Entries(root)
        .Where(e => Kind(e) == "Issued")
        .ToDictionary(e => BanId(e)!.Value, e => e.GetProperty("ban"));

    private static bool CanLift(Dictionary<Guid, JsonElement> bans, Guid banId) =>
        bans[banId].GetProperty("canLift").GetBoolean();

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
