using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The warning endpoints of moderation: who may read the list and write to it,
/// the issue of a warning and the change of its points.
/// </summary>
public class WarningControllerShould : WarningControllerTestBase
{
    public WarningControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUserWarningsPublicViewReturnsAggregatesWithoutModerationDetails()
    {
        // Seed directly: the create endpoint requires a moderator
        // identity, while the public GET must be exercised anonymously
        var warningId = Guid.NewGuid();
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Warnings.Add(new DbWarning
            {
                WarningId = warningId,
                TargetUserId = TestConstants.SecondUserId,
                AuthorId = TestConstants.ModeratorUserId,
                EntityId = Guid.Empty,
                EntityType = WarningEntityType.Unknown,
                CreatedUtc = DateTimeOffset.UtcNow.AddDays(-1),
                Text = "Секретная причина модерации",
                Points = 2,
                OriginalPoints = 2
            });
            await db.SaveChangesAsync();
        }

        try
        {
            // Anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/warnings");

            // Aggregates are present, moderation details are not
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            root.GetProperty("totalPoints").GetInt32().Should().Be(2);
            root.GetProperty("activeCount").GetInt32().Should().Be(1);

            var warning = root.GetProperty("warnings").EnumerateArray().Single();
            warning.GetProperty("points").GetInt32().Should().Be(2);
            warning.TryGetProperty("createdUtc", out _).Should().BeTrue();
            warning.TryGetProperty("reason", out _).Should().BeFalse();
            warning.TryGetProperty("moderator", out _).Should().BeFalse();
            warning.TryGetProperty("user", out _).Should().BeFalse();
            warning.TryGetProperty("entityId", out _).Should().BeFalse();
            warning.TryGetProperty("entityType", out _).Should().BeFalse();
            json.Should().NotContain("Секретная причина модерации");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Warnings.Where(w => w.WarningId == warningId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// The moderation list reads a page of the same log, evidence included.
    /// </summary>
    [Fact]
    public async Task ServeTheModerationListAPageWithTheEvidence()
    {
        var now = DateTimeOffset.UtcNow;
        var ids = Enumerable.Range(0x41, 3).Select(LogId).ToArray();
        await Seed(ids
            .Select((id, index) => Row(id, now.AddMinutes(index - 10), snapshot: $"Снимок {index}"))
            .ToArray());

        try
        {
            using var document = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get,
                $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}&skip=1&take=1",
                CustomWebApplicationFactory.CreateModeratorUser())));

            IdsOf(document).Should().Equal(ids[1]);
            document.RootElement.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(3);

            var row = document.RootElement.GetProperty("resources").EnumerateArray().Single();
            row.GetProperty("evidence").GetString().Should().Be("Снимок 1");
            row.TryGetProperty("entitySnapshot", out _).Should().BeFalse(
                "the source of the text never leaves the server, the rendering does");
            row.GetProperty("reason").GetString().Should().Be("Причина предупреждения");
        }
        finally
        {
            await Unseed(ids);
        }
    }

    [Fact]
    public async Task GetWarningsRequiresAuthentication()
    {
        var response = await Client.GetAsync("/v1/moderation/warnings");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWarningsWithAuthRequiresModeratorRole()
    {
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/warnings");

        var response = await Client.SendAsync(request);

        // Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// A moderator warns a reader, not a senior moderator or an administrator (ModerationRank); refused, nothing written.
    /// </summary>
    [Theory]
    [InlineData(TestConstants.SeniorModeratorUserLogin)]
    [InlineData(TestConstants.AdminUserLogin)]
    public async Task RefuseAModeratorAWarningToAHigherRank(string target)
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, "/v1/moderation/warnings", CustomWebApplicationFactory.CreateModeratorUser());
        request.Content = JsonContent.Create(new { username = target, reason = "Флуд", points = 1 });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Warnings.AnyAsync(w => w.Text == "Флуд" && w.AuthorId == TestConstants.ModeratorUserId))
            .Should().BeFalse();
    }

    /// <summary>
    /// A warning names a place only from the one list of them (WarningEntityType). A post of a game room, a
    /// message outside the global chat and any name the list has not got are refused, and nothing is written:
    /// nothing in a room or a conversation is moderated.
    /// </summary>
    [Theory]
    [InlineData("Post")]
    [InlineData("Message")]
    [InlineData("WebsiteTestimonial")]
    [InlineData("UserEndorsement")]
    [InlineData("Character")]
    public async Task RefuseAWarningOnAPlaceThatIsNotModerated(string entityType)
    {
        const string reason = "Оскорбление вне модерируемых мест";
        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, "/v1/moderation/warnings", CustomWebApplicationFactory.CreateModeratorUser());
        request.Content = JsonContent.Create(new
        {
            username = TestConstants.SecondUserLogin,
            reason,
            points = 1,
            entityType,
            entityId = Guid.NewGuid()
        });

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("entityType").EnumerateArray().Select(e => e.GetString())
            .Should().Equal("Это место не модерируется. Предупредите пользователя из профиля");
        await using var db = DatabaseFixture.CreateDbContext();
        (await db.Warnings.AnyAsync(w => w.Text == reason)).Should().BeFalse();
    }

    /// <summary>
    /// For what was done where nothing is moderated, the warning goes to the user alone: no place, no copy and
    /// no link, the points counted as any warning's. The open log draws it naming nothing.
    /// </summary>
    [Fact]
    public async Task IssueAWarningToTheUserWithNoPlace()
    {
        const string reason = "Оскорбления в личной переписке";
        var request = CreateAuthenticatedRequest(
            HttpMethod.Post, "/v1/moderation/warnings", CustomWebApplicationFactory.CreateModeratorUser());
        request.Content = JsonContent.Create(new { username = TestConstants.SecondUserLogin, reason, points = 1 });

        var response = await Client.SendAsync(request);

        try
        {
            response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var row = await db.Warnings.SingleAsync(w => w.Text == reason);
                row.EntityType.Should().Be(WarningEntityType.Unknown);
                row.EntityId.Should().Be(Guid.Empty);
                row.EntitySnapshot.Should().BeNull();
                row.Points.Should().Be(1);
            }

            using var log = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            var entry = log.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("reason").GetString() == reason);
            foreach (var field in new[] { "entityType", "evidence", "placeKind", "placeTitle", "entityUrl" })
            {
                entry.TryGetProperty(field, out _).Should().BeFalse("{0} names a place, and there is none", field);
            }
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Warnings.Where(w => w.Text == reason).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// A body that leaves the points out is refused, not read as zero.
    /// </summary>
    /// <remarks>Absence means "leave alone" in a patch, and zero takes the warning off the scale.</remarks>
    [Fact]
    public async Task RefuseAChangeThatNamesNoPoints()
    {
        var warningId = LogId(0x66);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 3));

        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/moderation/warnings/{warningId}", CustomWebApplicationFactory.CreateSeniorModeratorUser());
            request.Content = JsonContent.Create(new { reason = "Баллы тут ни при чем" });
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.Points.Should().Be(3, "a patch without the field must leave the points alone");
            stored.PointsChangedUtc.Should().BeNull("nothing was corrected");
        }
        finally
        {
            await Unseed(warningId);
        }
    }

    private Task<HttpResponseMessage> ChangePoints(Guid warningId, int points, string reason, GeneralUser user)
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Patch, $"/v1/moderation/warnings/{warningId}", user);
        request.Content = JsonContent.Create(new { points, reason });
        return Client.SendAsync(request);
    }

    /// <summary>
    /// A moderator who did not issue the warning cannot touch its points.
    /// </summary>
    [Fact]
    public async Task RefuseTheChangeToAModeratorWhoDidNotIssueTheWarning()
    {
        var warningId = LogId(0x61);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 3,
            authorId: TestConstants.SeniorModeratorUserId));

        try
        {
            var response = await ChangePoints(warningId, 0, "Считаю решение неверным",
                CustomWebApplicationFactory.CreateModeratorUser());

            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.Points.Should().Be(3, "the refusal must leave the warning as it was");
            stored.PointsChangedByUserId.Should().BeNull();
        }
        finally
        {
            await Unseed(warningId);
        }
    }

    /// <summary>
    /// The senior moderator takes the points off, the warning stays, and the row
    /// carries who did it, when and why.
    /// </summary>
    [Fact]
    public async Task WriteTheSecondSignatureWhenTheSeniorModeratorZeroesThePoints()
    {
        var warningId = LogId(0x62);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 3));

        try
        {
            var response = await ChangePoints(
                warningId, 0, "Нарушения не было, модератор ошибся", CustomWebApplicationFactory.CreateSeniorModeratorUser());

            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);

            using var document = JsonDocument.Parse(body);
            var resource = document.RootElement.GetProperty("resource");
            resource.GetProperty("points").GetInt32().Should().Be(0);
            resource.GetProperty("originalPoints").GetInt32().Should().Be(3);
            resource.GetProperty("pointsChangedBy").GetString()
                .Should().Be(TestConstants.SeniorModeratorUserLogin);
            resource.GetProperty("pointsChangeText").GetString()
                .Should().Be("Нарушения не было, модератор ошибся");

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.Points.Should().Be(0);
            stored.OriginalPoints.Should().Be(3, "what the warning was issued with is not overwritten");
            stored.PointsChangedByUserId.Should().Be(TestConstants.SeniorModeratorUserId);
            stored.PointsChangedUtc.Should().NotBeNull();
            stored.Text.Should().Be("Причина предупреждения", "the warning itself is not rewritten");
        }
        finally
        {
            await Unseed(warningId);
        }
    }

    /// <summary>
    /// The scale counts the points as they stand, so a warning corrected to zero stays in the log and weighs nothing.
    /// </summary>
    [Fact]
    public async Task CountTheUserTotalByTheChangedPoints()
    {
        var warningId = LogId(0x63);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 4));

        try
        {
            using (var before = await ReadDocument(
                await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/warnings")))
            {
                before.RootElement.GetProperty("totalPoints").GetInt32().Should().Be(4);
            }

            (await ChangePoints(warningId, 0, "Нарушения не было", CustomWebApplicationFactory.CreateSeniorModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            using var after = await ReadDocument(
                await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/warnings"));
            after.RootElement.GetProperty("totalPoints").GetInt32().Should().Be(0);
            after.RootElement.GetProperty("warnings").EnumerateArray().Should().ContainSingle(
                "the warning stays on the profile with zero points, it is not withdrawn");
        }
        finally
        {
            await Unseed(warningId);
        }
    }

    /// <summary>
    /// A warning nobody issued is answered with 404 rather than with a write.
    /// </summary>
    [Fact]
    public async Task AnswerTheChangeOfAMissingWarningWithNotFound()
    {
        var response = await ChangePoints(
            LogId(0x64), 0, "Ошибка модератора", CustomWebApplicationFactory.CreateSeniorModeratorUser());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// The explanation of the change is required, and the points stay inside the
    /// scale.
    /// </summary>
    [Theory]
    [InlineData(0, "")]
    [InlineData(7, "Слишком много баллов")]
    public async Task RefuseAChangeOutsideTheRules(int points, string reason)
    {
        var warningId = LogId(0x65);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 2));

        try
        {
            var response = await ChangePoints(warningId, points, reason, CustomWebApplicationFactory.CreateSeniorModeratorUser());

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.Points.Should().Be(2);
        }
        finally
        {
            await Unseed(warningId);
        }
    }
}
