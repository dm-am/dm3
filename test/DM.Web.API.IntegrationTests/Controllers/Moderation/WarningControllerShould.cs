using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbChat = DM.Infrastructure.Persistence.Entities.Messaging.Chat;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbCommentEdit = DM.Infrastructure.Persistence.Entities.Shared.CommentEdit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbGameAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.GameAttributeSchema;
using DM.Web.API.IntegrationTests.Helpers;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// Integration tests for WarningController
/// </summary>
public class WarningControllerShould : IntegrationTestBase
{
    public WarningControllerShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task GetUserWarnings_PublicView_ReturnsAggregatesWithoutModerationDetails()
    {
        // Arrange - seed directly: the create endpoint requires a moderator
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
            // Act - anonymous request
            var response = await Client.GetAsync($"/v1/users/{TestConstants.SecondUserLogin}/warnings");

            // Assert - aggregates are present, moderation details are not
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
    /// Identifiers that order the same way in PostgreSQL and in .NET.
    /// </summary>
    /// <remarks>
    /// The database compares a uuid byte by byte over all sixteen bytes, while
    /// Guid.CompareTo reads the first eight as three little-endian numbers. Rows
    /// that differ only in the last byte are ordered identically by both, so a test
    /// about the order of a tie can be written down without saying which side sorted.
    /// </remarks>
    private static Guid LogId(int lastByte) =>
        Guid.Parse($"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb{lastByte:x2}");

    /// <summary>One warning, written straight into the table.</summary>
    /// <remarks>
    /// The create endpoint needs a moderator identity and raises an event; the log
    /// is read anonymously and says nothing about how a row got there.
    /// </remarks>
    private static DbWarning Row(
        Guid warningId,
        DateTimeOffset createdUtc,
        Guid? targetUserId = null,
        string text = "Причина предупреждения",
        int points = 1,
        int? originalPoints = null,
        string? snapshot = null,
        Guid? authorId = null,
        Guid? entityId = null,
        WarningEntityType entityType = WarningEntityType.Unknown,
        Guid? pointsChangedByUserId = null,
        DateTimeOffset? pointsChangedUtc = null,
        string? pointsChangeText = null)
        => new()
        {
            WarningId = warningId,
            TargetUserId = targetUserId ?? TestConstants.SecondUserId,
            AuthorId = authorId ?? TestConstants.ModeratorUserId,
            EntityId = entityId ?? Guid.Empty,
            EntityType = entityType,
            CreatedUtc = createdUtc,
            Text = text,
            Points = points,
            OriginalPoints = originalPoints ?? points,
            EntitySnapshot = snapshot,
            PointsChangedByUserId = pointsChangedByUserId,
            PointsChangedUtc = pointsChangedUtc,
            PointsChangeText = pointsChangeText
        };

    private async Task Seed(params DbWarning[] rows)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Warnings.AddRange(rows);
        await db.SaveChangesAsync();
    }

    private async Task Unseed(params Guid[] ids)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Warnings.Where(w => ids.Contains(w.WarningId)).ExecuteDeleteAsync();
    }

    private static async Task<JsonDocument> ReadDocument(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
        return JsonDocument.Parse(body);
    }

    private static List<Guid> IdsOf(JsonDocument document) => document.RootElement
        .GetProperty("resources").EnumerateArray()
        .Select(row => row.GetProperty("id").GetGuid())
        .ToList();

    /// <summary>
    /// The log is the page the rules link to, and it is read without an account.
    /// </summary>
    /// <remarks>
    /// A copy of a text with no place the server can name is shown all the same,
    /// as the text the warning was given for (the owner, 05.10: "Всегда
    /// показывай"), with no place and no address: there is nothing to name and
    /// nowhere to lead.
    /// </remarks>
    [Fact]
    public async Task ShowThePlacelessTextInTheOpenLog_WithoutAPlaceOrAnAddress()
    {
        var shown = LogId(0x01);
        await Seed(
            Row(shown, DateTimeOffset.UtcNow.AddMinutes(-1), points: 2,
                text: "Оскорбление в теме", snapshot: "Снимок оскорбления"));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));

            var row = document.RootElement.GetProperty("resources").EnumerateArray().Single();
            row.GetProperty("id").GetGuid().Should().Be(shown);
            row.GetProperty("points").GetInt32().Should().Be(2);
            row.GetProperty("reason").GetString().Should().Be("Оскорбление в теме");
            row.GetProperty("user").GetProperty("username").GetString()
                .Should().Be(TestConstants.SecondUserLogin);
            row.GetProperty("moderator").GetProperty("username").GetString()
                .Should().Be(TestConstants.ModeratorUserLogin);
            row.GetProperty("evidence").GetString().Should().Contain("Снимок оскорбления");

            // The fields of a correction and of the place are absent rather than
            // null: the serialiser drops nulls.
            row.EnumerateObject().Select(field => field.Name).Should().BeEquivalentTo(
                ["id", "user", "moderator", "points", "originalPoints", "reason", "createdUtc", "evidence"],
                "a row whose text cannot be placed shows the text and nothing about a place");
        }
        finally
        {
            await Unseed(shown);
        }
    }

    /// <summary>
    /// The text of a place a guest can read is shown as the place shows it:
    /// rendered, with the place, its address and the date of the text.
    /// </summary>
    /// <remarks>
    /// The snapshot is the source as it was written, and the source of a text may
    /// carry a hidden block its page never shows to anybody. The log gets the
    /// rendering and never the source: no tag, and no hidden line.
    /// </remarks>
    [Fact]
    public async Task ShowTheRenderedTextOfAnOpenPlaceInTheLog()
    {
        var warningId = LogId(0x02);
        var messageId = Guid.NewGuid();
        const string source = "[b]Жирная реплика[/b] и [private=\"seconduser\"]СКРЫТЫЙ ТЕКСТ[/private]";
        await SeedGlobalChatMessage(messageId, source);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: messageId, entityType: WarningEntityType.Message, snapshot: source));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));

            var row = document.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);
            var evidence = row.GetProperty("evidence").GetString();
            evidence.Should().Contain("<strong>Жирная реплика</strong>")
                .And.NotContain("[b]", "the log shows the text, not its source")
                .And.NotContain("СКРЫТЫЙ ТЕКСТ", "a hidden block is shown to nobody outside a game post");
            row.GetProperty("entityType").GetString().Should().Be("Message");
            row.GetProperty("placeKind").GetString().Should().Be("GlobalChat");
            row.GetProperty("entityUrl").GetString().Should().Be($"/global-chat#msg-{messageId}");
            row.TryGetProperty("entityCreatedUtc", out _).Should().BeTrue();
        }
        finally
        {
            await Unseed(warningId);
            await UnseedMessage(messageId);
        }
    }

    /// <summary>
    /// A text standing where a guest cannot read it is shown in the open log all
    /// the same, with its place, and without the address a guest could not
    /// follow; the moderation list has the address. A hidden block in it is
    /// erased for both, as on every surface that does not declare one.
    /// </summary>
    /// <remarks>
    /// The text used to stay out of the log, "Текст виден только модераторам" in
    /// its place, and the owner did away with it (05.10): "Всегда показывай".
    /// </remarks>
    [Fact]
    public async Task ShowTheTextOfAHiddenGameInTheOpenLog_WithoutTheAddress()
    {
        var warningId = LogId(0x03);
        var commentId = Guid.NewGuid();
        const string offending = "Мастер, ты вообще в своем уме";
        const string source = offending + " [private]СКРЫТЫЙ ТЕКСТ[/private]";
        var hiddenGameId = await SeedPrivateDraftGame();
        await SeedGameComment(commentId, hiddenGameId, source);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: commentId, entityType: WarningEntityType.Comment, snapshot: source));

        try
        {
            using (var document = await ReadDocument(
                       await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}")))
            {
                var row = document.RootElement.GetProperty("resources").EnumerateArray()
                    .Single(item => item.GetProperty("id").GetGuid() == warningId);
                row.GetProperty("evidence").GetString().Should().Contain(offending)
                    .And.NotContain("СКРЫТЫЙ ТЕКСТ", "a hidden block is shown to nobody outside a game post");
                row.GetProperty("placeKind").GetString().Should().Be("Game");
                row.GetProperty("placeTitle").GetString().Should().Be(PrivateDraftTitle);
                row.TryGetProperty("entityUrl", out _).Should().BeFalse(
                    "a private draft is open to its people alone");
                row.TryGetProperty("evidenceHidden", out _).Should().BeFalse();
            }

            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            var moderated = moderation.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);
            moderated.GetProperty("evidence").GetString().Should().Contain(offending)
                .And.NotContain("СКРЫТЫЙ ТЕКСТ", "the moderator reading the list is no exception");
            moderated.GetProperty("entityUrl").GetString().Should().EndWith($"#comment-{commentId}");
            moderated.GetProperty("placeKind").GetString().Should().Be("Game");
            moderated.GetProperty("placeTitle").GetString().Should().Be(PrivateDraftTitle);
            moderated.TryGetProperty("logClosure", out _).Should().BeFalse();
        }
        finally
        {
            await Unseed(warningId);
            await UnseedComment(commentId);
        }
    }

    /// <summary>
    /// What moderation took off the site and what its author deleted are both
    /// shown, as the text the warning was given for, with the place and the date
    /// of the text and without an address to follow.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowADeletedTextInTheOpenLog_WithoutAnAddress(bool deletedByModeration)
    {
        var warningId = LogId(deletedByModeration ? 0x04 : 0x05);
        var messageId = Guid.NewGuid();
        const string offending = "Реплика, удаленная после предупреждения";
        await SeedGlobalChatMessage(messageId, offending, deletedBy: deletedByModeration
            ? TestConstants.ModeratorUserId
            : TestConstants.SecondUserId);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 1,
            entityId: messageId, entityType: WarningEntityType.Message, snapshot: offending));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            var row = document.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);

            row.GetProperty("evidence").GetString().Should().Contain(offending);
            row.GetProperty("placeKind").GetString().Should().Be("GlobalChat");
            row.TryGetProperty("entityUrl", out _).Should().BeFalse("a deleted text has no page to open");
            row.TryGetProperty("entityCreatedUtc", out _).Should().BeTrue(
                "the date of the text outlives its deletion");
        }
        finally
        {
            await Unseed(warningId);
            await UnseedMessage(messageId);
        }
    }

    /// <summary>
    /// A text its author rewrote after the warning is shown in both logs as the
    /// words the warning was given for, and neither row says it was rewritten.
    /// </summary>
    /// <remarks>
    /// A warning is about the text it was issued for, and an edit made on the
    /// site afterwards is no part of it. The rows used to carry the moment of
    /// such an edit, which the log drew as "Отредактировано" and a date beside
    /// a text that had not been edited at all: the snapshot never changes. The
    /// client dropped the mark and the server dropped the field, so the edit
    /// history of the object is not read for a date nobody shows.
    /// </remarks>
    [Fact]
    public async Task ShowTheWarnedTextAfterAnEdit_WithoutSayingItWasEdited()
    {
        var warningId = LogId(0x07);
        var commentId = Guid.NewGuid();
        var issued = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero);
        const string offending = "Текст до правки";
        const string rewritten = "Текст после правки";
        await SeedTopicComment(commentId, rewritten);
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.CommentEdits.Add(new DbCommentEdit
            {
                CommentEditId = Guid.NewGuid(),
                CommentId = commentId,
                EditorUserId = TestConstants.SecondUserId,
                EditedUtc = issued.AddHours(1)
            });
            await db.SaveChangesAsync();
        }

        await Seed(Row(warningId, issued, points: 1,
            entityId: commentId, entityType: WarningEntityType.Comment, snapshot: offending));

        try
        {
            using (var document = await ReadDocument(
                       await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}")))
            {
                var row = document.RootElement.GetProperty("resources").EnumerateArray()
                    .Single(item => item.GetProperty("id").GetGuid() == warningId);

                row.GetProperty("evidence").GetString().Should().Contain(offending)
                    .And.NotContain(rewritten, "the log shows the text the warning was given for");
                row.GetProperty("placeKind").GetString().Should().Be("Topic");
                row.TryGetProperty("entityEditedUtc", out _).Should().BeFalse(
                    "the open log says nothing of an edit made after the warning");
            }

            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            var moderated = moderation.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);

            moderated.GetProperty("evidence").GetString().Should().Contain(offending)
                .And.NotContain(rewritten, "moderation reads the same text the open log does");
            moderated.GetProperty("entityUrl").GetString().Should().EndWith($"#comment-{commentId}");
            moderated.TryGetProperty("entityEditedUtc", out _).Should().BeFalse(
                "the moderation list says nothing of an edit made after the warning either");
        }
        finally
        {
            await Unseed(warningId);
            await using var db = DatabaseFixture.CreateDbContext();
            await db.CommentEdits.Where(e => e.CommentId == commentId).ExecuteDeleteAsync();
            await UnseedComment(commentId);
        }
    }

    /// <summary>
    /// A warning issued from a profile names no content, and the row says so.
    /// </summary>
    [Fact]
    public async Task AnswerAProfileWarningWithoutAnyText()
    {
        var warningId = LogId(0x06);
        await Seed(Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), points: 0));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            var row = document.RootElement.GetProperty("resources").EnumerateArray()
                .Single(item => item.GetProperty("id").GetGuid() == warningId);

            row.TryGetProperty("entityType", out _).Should().BeFalse();
            row.TryGetProperty("evidence", out _).Should().BeFalse();
            row.TryGetProperty("placeKind", out _).Should().BeFalse(
                "there was no text, and the log must not say there was");
        }
        finally
        {
            await Unseed(warningId);
        }
    }

    private async Task SeedGlobalChatMessage(Guid messageId, string text, Guid? deletedBy = null)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Messages.Add(new DbMessage
        {
            MessageId = messageId,
            ChatId = DbChat.GlobalChatId,
            UserId = TestConstants.SecondUserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1),
            IsRemoved = deletedBy.HasValue,
            DeletedByUserId = deletedBy,
            DeletedUtc = deletedBy.HasValue ? DateTimeOffset.UtcNow : null
        });
        await db.SaveChangesAsync();
    }

    private async Task UnseedMessage(Guid messageId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Messages.IgnoreQueryFilters().Where(m => m.MessageId == messageId).ExecuteDeleteAsync();
    }

    private const string PrivateDraftTitle = "Игра в оформлении";

    /// <summary>
    /// A game a guest cannot open: a draft its master did not open to everybody,
    /// led by a master of its own. Fresh identifiers every time, the fixture
    /// database is shared and its public ids are uniquely indexed.
    /// </summary>
    private async Task<Guid> SeedPrivateDraftGame()
    {
        await using var db = DatabaseFixture.CreateDbContext();
        var masterId = Guid.NewGuid();
        db.Users.Add(new DbUser
        {
            UserId = masterId,
            Username = $"wpd{masterId:N}"[..20],
            Email = $"{masterId:N}@warning-draft.example",
            PasswordHash = "hash",
            Salt = "salt"
        });
        var game = new DbGame
        {
            GameId = Guid.NewGuid(),
            PublicId = Guid.NewGuid().ToString("N")[..10],
            MasterId = masterId,
            Title = PrivateDraftTitle,
            Status = ModuleStatus.Draft,
            PremoderationStatus = PremoderationStatus.Approved,
            DraftVisibility = DraftVisibility.Private,
            CommentsAccessMode = CommentsAccessMode.Public,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        db.Games.Add(game);
        db.Set<DbGameAttributeSchema>().Add(GameSheetTestHelper.Sheet(game.GameId));
        await db.SaveChangesAsync();
        return game.GameId;
    }

    private async Task SeedGameComment(Guid commentId, Guid gameId, string text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = gameId,
            AuthorId = TestConstants.SecondUserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-1)
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedTopicComment(Guid commentId, string text)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = TestConstants.TestTopicId,
            AuthorId = TestConstants.SecondUserId,
            Text = text,
            CreatedUtc = DateTimeOffset.UtcNow.AddHours(-3)
        });
        await db.SaveChangesAsync();
    }

    private async Task UnseedComment(Guid commentId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Comments.IgnoreQueryFilters().Where(c => c.CommentId == commentId).ExecuteDeleteAsync();
    }

    /// <summary>
    /// A warning whose points were taken off stays in the open log, and the log
    /// carries the second signature under it.
    /// </summary>
    /// <remarks>
    /// The row used to be soft-deleted instead: undoing a warning erased it from
    /// the log, so a reader could not tell a warning that was never issued from
    /// one a senior moderator had found unjustified. Nothing is erased now, and
    /// the correction is shown to everybody who reads the log — the owner's
    /// decision, and the reason the explanation is required of the change.
    /// </remarks>
    [Fact]
    public async Task KeepACorrectedWarningInTheOpenLogWithBothSignatures()
    {
        var corrected = LogId(0x51);
        var changedUtc = new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero);
        await Seed(Row(corrected, DateTimeOffset.UtcNow.AddMinutes(-1),
            text: "Спойлер без тега в общем чате", points: 0, originalPoints: 2,
            pointsChangedByUserId: TestConstants.SeniorModeratorUserId,
            pointsChangedUtc: changedUtc,
            pointsChangeText: "Тега не требовалось, баллы сняты"));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));

            var row = document.RootElement.GetProperty("resources").EnumerateArray().Single();
            row.GetProperty("id").GetGuid().Should().Be(corrected);
            row.GetProperty("points").GetInt32().Should().Be(0);
            row.GetProperty("originalPoints").GetInt32().Should().Be(2);
            row.GetProperty("reason").GetString().Should().Be("Спойлер без тега в общем чате");
            row.GetProperty("pointsChangedBy").GetString()
                .Should().Be(TestConstants.SeniorModeratorUserLogin);
            row.GetProperty("pointsChangeText").GetString()
                .Should().Be("Тега не требовалось, баллы сняты");
            row.GetProperty("pointsChangedUtc").GetDateTimeOffset().Should().Be(changedUtc);
        }
        finally
        {
            await Unseed(corrected);
        }
    }

    /// <summary>
    /// A name nobody carries narrows the log to nothing rather than refusing.
    /// </summary>
    [Fact]
    public async Task AnswerAnUnknownNameWithAnEmptyLog()
    {
        var mine = LogId(0x11);
        await Seed(Row(mine, DateTimeOffset.UtcNow));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync("/v1/warnings?username=nobodycarriesthisname"));

            IdsOf(document).Should().BeEmpty();
            document.RootElement.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(0);
        }
        finally
        {
            await Unseed(mine);
        }
    }

    /// <summary>
    /// The page a caller asked for, and the total the page control needs.
    /// </summary>
    /// <remarks>
    /// Asserted by the rows on the page rather than by the echo of skip: the envelope
    /// rounds its echo to the start of the page it computed.
    /// </remarks>
    [Fact]
    public async Task ServeTheRowsTheSkipAndTakeAskFor()
    {
        var now = DateTimeOffset.UtcNow;
        var ids = Enumerable.Range(0x21, 5).Select(LogId).ToArray();
        // Newest last, so the log answers in the reverse of this order
        await Seed(ids.Select((id, index) => Row(id, now.AddMinutes(index - 10))).ToArray());

        try
        {
            using var document = await ReadDocument(await Client.GetAsync(
                $"/v1/warnings?username={TestConstants.SecondUserLogin}&skip=1&take=2"));

            IdsOf(document).Should().Equal(ids[3], ids[2]);
            document.RootElement.GetProperty("paging").GetProperty("total").GetInt32().Should().Be(5);
            document.RootElement.GetProperty("paging").GetProperty("take").GetInt32().Should().Be(2);
        }
        finally
        {
            await Unseed(ids);
        }
    }

    /// <summary>
    /// Warnings written in the same millisecond stand in one order, on every read.
    /// </summary>
    /// <remarks>
    /// Ordered by the moment alone, a tie is resolved by whatever the database
    /// happens to return, and the two reads a page control makes may disagree: one
    /// warning shows up on both pages and another on neither. Equal timestamps are
    /// not a curiosity here: the DM2 import carries them by the thousand.
    ///
    /// The page read is the one the tie runs through, and the whole sequence on it
    /// is asserted: a check that only counts the rows passes on the broken ordering
    /// as readily as on the fixed one.
    /// </remarks>
    [Fact]
    public async Task OrderWarningsOfTheSameMomentByIdentifier()
    {
        var tied = new DateTimeOffset(2024, 5, 17, 12, 30, 0, TimeSpan.Zero);
        var ids = Enumerable.Range(0x31, 5).Select(LogId).ToArray();
        await Seed(ids.Select(id => Row(id, tied)).ToArray());

        try
        {
            using var document = await ReadDocument(await Client.GetAsync(
                $"/v1/warnings?username={TestConstants.SecondUserLogin}&skip=1&take=3"));

            IdsOf(document).Should().Equal(ids[3], ids[2], ids[1]);
        }
        finally
        {
            await Unseed(ids);
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
    public async Task GetWarnings_RequiresAuthentication()
    {
        // Act
        var response = await Client.GetAsync("/v1/moderation/warnings");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetWarnings_WithAuth_RequiresModeratorRole()
    {
        // Arrange
        var request = CreateAuthenticatedRequest(HttpMethod.Get, "/v1/moderation/warnings");

        // Act
        var response = await Client.SendAsync(request);

        // Assert - Regular user should be forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostWarning_RequiresAuthentication()
    {
        // Arrange
        var warningData = new
        {
            userLogin = TestConstants.SecondUserLogin,
            reason = "Misconduct",
            description = "Test warning"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/v1/moderation/warnings", warningData);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A moderator warns a reader and not a senior moderator or an administrator
    /// (ModerationRank). The refusal is said and nothing is written: the profile
    /// and the content offer no button for it, so the write is the last line.
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

    [Fact]
    public async Task PatchWarning_RequiresAuthentication()
    {
        var warningId = Guid.NewGuid();

        var response = await Client.PatchAsJsonAsync(
            $"/v1/moderation/warnings/{warningId}", new { points = 0, reason = "Ошибка модератора" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>A change of points as one of the moderators.</summary>
    /// <summary>
    /// A body that leaves the points out is refused, not read as zero.
    /// </summary>
    /// <remarks>
    /// Absence means "leave alone" in a patch, and zero is the one value that
    /// takes the warning off the violation scale entirely. Bound as a plain int,
    /// the missing field arrived as zero and passed the range check, so a body
    /// carrying nothing but a reason quietly cleared the points.
    /// </remarks>
    [Fact]
    public async Task RefuseAChangeThatNamesNoPoints()
    {
        var warningId = LogId(0x66);
        await Seed(Row(warningId, DateTimeOffset.UtcNow, points: 3));

        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Patch, $"/v1/moderation/warnings/{warningId}", SeniorModerator());
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

    private static GeneralUser SeniorModerator() => new()
    {
        UserId = TestConstants.SeniorModeratorUserId,
        Username = TestConstants.SeniorModeratorUserUsername,
        Role = UserRole.SeniorModerator
    };

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
                warningId, 0, "Нарушения не было, модератор ошибся", SeniorModerator());

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
    /// The points of the warned user are counted by the points as they stand
    /// now, so a warning corrected to zero stops weighing on the scale while
    /// staying in the log.
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

            (await ChangePoints(warningId, 0, "Нарушения не было", SeniorModerator()))
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
            LogId(0x64), 0, "Ошибка модератора", SeniorModerator());

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
            var response = await ChangePoints(warningId, points, reason, SeniorModerator());

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

    /// <summary>
    /// An avatar of a user, written straight into the table: replaced by its
    /// owner (deleted with no author) unless the test says otherwise.
    /// </summary>
    private static DbUpload Avatar(Guid uploadId, Guid ownerId, bool removed = true, Guid? deletedBy = null) => new()
    {
        UploadId = uploadId,
        CreatedUtc = DateTimeOffset.UtcNow.AddDays(-30),
        ConfirmedUtc = DateTimeOffset.UtcNow.AddDays(-30),
        UserId = ownerId,
        TargetUserId = ownerId,
        Type = UploadType.UserAvatar,
        Status = UploadStatus.Confirmed,
        Original = true,
        ContentType = "image/png",
        SizeBytes = 2048,
        Width = 731,
        Height = 793,
        ObjectKey = $"avatars/{ownerId:N}_{uploadId:N}.png",
        FilePath = $"https://cdn.example.test/dm/avatars/{ownerId:N}_{uploadId:N}.png",
        IsRemoved = removed,
        DeletedByUserId = removed ? deletedBy : null,
        DeletedUtc = removed ? DateTimeOffset.UtcNow.AddDays(-10) : null
    };

    /// <summary>A warning on the profile of the user, the way the server writes one.</summary>
    private static DbWarning OnProfile(
        Guid warningId, Guid targetUserId, string? status, string? info, Guid? avatarUploadId,
        string text = "Неприемлемый аватар")
    {
        var row = Row(warningId, DateTimeOffset.UtcNow.AddMinutes(-1), targetUserId: targetUserId, text: text,
            points: 3, entityId: targetUserId, entityType: WarningEntityType.Profile, snapshot: info);
        row.ProfileStatusSnapshot = status;
        row.ProfileAvatarUploadId = avatarUploadId;
        return row;
    }

    private async Task SeedUpload(DbUpload upload)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        db.Uploads.Add(upload);
        await db.SaveChangesAsync();
    }

    private async Task UnseedUpload(Guid uploadId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        await db.Uploads.IgnoreQueryFilters().Where(u => u.UploadId == uploadId).ExecuteDeleteAsync();
    }

    private static JsonElement RowOf(JsonDocument document, Guid warningId) => document.RootElement
        .GetProperty("resources").EnumerateArray()
        .Single(item => item.GetProperty("id").GetGuid() == warningId);

    /// <summary>
    /// A warning issued on a profile shows the profile as it stood then, in the
    /// open log and in the moderation list alike: the status, "О себе" rendered
    /// as the profile renders it, and the avatar, although its owner has since
    /// replaced it.
    /// </summary>
    /// <remarks>
    /// The row used to show nothing but "Профиль пользователя" (the owner, 12.10,
    /// row 41: "почему профиль пользователя не видно?"): the server kept neither
    /// the status nor the picture, and the sweeper erased the replaced avatar a day
    /// later. The object of such a warning is the warned user himself, so the row
    /// names no place and no address, and "О себе" is not given as the evidence of
    /// a text. The reason is rendered as well: the moderator writes it in the
    /// markup of the site, and the log used to show the tags.
    /// </remarks>
    [Fact]
    public async Task ShowTheProfileAWarningWasIssuedForInBothLogs()
    {
        var warningId = LogId(0x71);
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.SecondUserId);
        await SeedUpload(avatar);
        await Seed(OnProfile(warningId, TestConstants.SecondUserId,
            "Статус на момент выдачи", "[b]О себе[/b] на момент выдачи", uploadId,
            text: "[b]Неприемлемый[/b] аватар"));

        try
        {
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));

            var rows = new[] { ("open log", RowOf(open, warningId)), ("moderation", RowOf(moderation, warningId)) };
            foreach (var (name, row) in rows)
            {
                row.GetProperty("entityType").GetString().Should().Be("Profile", name);
                row.GetProperty("reason").GetString().Should().Contain("<strong>Неприемлемый</strong>")
                    .And.NotContain("[b]", "{0} shows the reason, not its source", name);
                row.TryGetProperty("evidence", out _).Should().BeFalse(
                    "{0}: the words of a profile are given with the profile, not as a text", name);
                row.TryGetProperty("entityUrl", out _).Should().BeFalse("{0}: a profile has no place to link", name);
                row.TryGetProperty("placeKind", out _).Should().BeFalse(name);
                row.TryGetProperty("entityCreatedUtc", out _).Should().BeFalse(name);

                var profile = row.GetProperty("profile");
                profile.GetProperty("status").GetString().Should().Be("Статус на момент выдачи", name);
                profile.GetProperty("info").GetString().Should().Contain("<strong>О себе</strong>")
                    .And.NotContain("[b]", "{0} renders О себе as the profile does", name);
                var picture = profile.GetProperty("picture");
                picture.GetProperty("originalUrl").GetString().Should().Be(avatar.FilePath,
                    "{0} keeps the avatar its owner replaced after the warning", name);
                picture.GetProperty("originalWidth").GetInt32().Should().Be(731, name);
                picture.GetProperty("originalHeight").GetInt32().Should().Be(793, name);
            }

            RowOf(moderation, warningId).GetProperty("entityId").GetGuid().Should().Be(TestConstants.SecondUserId);
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// The pinned avatar is kept from its owner and not from moderation: a
    /// deletion by anybody but the owner takes the picture out of the answer at
    /// once, before the sweeper gets to the file, and the status stays.
    /// </summary>
    [Theory]
    [InlineData("still the avatar", true)]
    [InlineData("replaced by its owner", true)]
    [InlineData("deleted by its owner", true)]
    [InlineData("deleted by moderation", false)]
    public async Task ShowThePinnedPictureUntilModerationDeletesIt(string story, bool shown)
    {
        var warningId = LogId(0x72);
        var uploadId = Guid.NewGuid();
        var avatar = story switch
        {
            "still the avatar" => Avatar(uploadId, TestConstants.SecondUserId, removed: false),
            "replaced by its owner" => Avatar(uploadId, TestConstants.SecondUserId),
            "deleted by its owner" => Avatar(uploadId, TestConstants.SecondUserId,
                deletedBy: TestConstants.SecondUserId),
            _ => Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.ModeratorUserId)
        };
        await SeedUpload(avatar);
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId));

        try
        {
            using var document = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            var profile = RowOf(document, warningId).GetProperty("profile");

            profile.TryGetProperty("picture", out _).Should().Be(shown, "the avatar is {0}", story);
            profile.GetProperty("status").GetString().Should().Be("Статус",
                "the status stays whatever became of the picture");
            profile.TryGetProperty("info", out _).Should().BeFalse("a profile without О себе gives none");
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// Nothing of the profile of a removed account is given, in either log.
    /// </summary>
    /// <remarks>
    /// A removed account shows nothing of itself anywhere, its avatar included.
    /// Today the read loses the whole row of a removed account (the join onto the
    /// account under its filter, RemovedAuthorRowsShould), and the projection
    /// gives no profile for one on its own, so the snapshot stays hidden when the
    /// read starts keeping such rows.
    /// </remarks>
    [Fact]
    public async Task GiveNoProfileOfARemovedAccount()
    {
        var warningId = LogId(0x73);
        var uploadId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var status = $"Статус удаленного {removedId:N}";
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            db.Users.Add(new DbUser
            {
                UserId = removedId,
                Username = $"gone{removedId:N}"[..20],
                Email = $"{removedId:N}@removed.example.com",
                PasswordHash = "hash",
                Salt = "salt",
                CreatedUtc = DateTimeOffset.UtcNow.AddYears(-1),
                IsRemoved = true
            });
            await db.SaveChangesAsync();
        }

        await SeedUpload(Avatar(uploadId, removedId));
        await Seed(OnProfile(warningId, removedId, status, "О себе удаленного", uploadId));

        try
        {
            var open = await Client.GetAsync("/v1/warnings?take=100");
            var moderation = await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, "/v1/moderation/warnings?take=100", CustomWebApplicationFactory.CreateModeratorUser()));

            foreach (var response in new[] { open, moderation })
            {
                var body = await response.Content.ReadAsStringAsync();
                response.StatusCode.Should().Be(HttpStatusCode.OK, "body was: {0}", body);
                body.Should().NotContain(status).And.NotContain("О себе удаленного")
                    .And.NotContain(uploadId.ToString("N"), "nothing of a removed account is shown");
            }
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Users.IgnoreQueryFilters().Where(u => u.UserId == removedId).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// The upload row going away clears the column that pinned it, and the
    /// warning stays.
    /// </summary>
    /// <remarks>
    /// The sweeper deletes the row of a picture moderation deleted, once its grace
    /// period is over. The key is what keeps the warning from holding an
    /// identifier of nothing, and from refusing the delete altogether.
    /// </remarks>
    [Fact]
    public async Task ClearThePinWhenTheUploadIsGone()
    {
        var warningId = LogId(0x74);
        var uploadId = Guid.NewGuid();
        await SeedUpload(Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.ModeratorUserId));
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId));

        try
        {
            await UnseedUpload(uploadId);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.ProfileAvatarUploadId.Should().BeNull();
            stored.ProfileStatusSnapshot.Should().Be("Статус", "the warning outlives its picture");
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// A warning on a profile is a warning on the warned user: the identifier the
    /// request names is not read, and the profile is copied as it stands at the
    /// moment of issue, the avatar pinned by its upload.
    /// </summary>
    [Fact]
    public async Task CopyTheProfileOfTheWarnedUserWhicheverIdentifierTheRequestNamed()
    {
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.SecondUserId, removed: false);
        await SeedUpload(avatar);
        string? statusBefore;
        string? infoBefore;
        Guid? avatarBefore;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
            (statusBefore, infoBefore, avatarBefore) = (user.Status, user.Info, user.AvatarUploadId);
            user.Status = "Статус перед выдачей";
            user.Info = "[i]О себе[/i] перед выдачей";
            user.AvatarUploadId = uploadId;
            await db.SaveChangesAsync();
        }

        Guid? warningId = null;
        try
        {
            var request = CreateAuthenticatedRequest(
                HttpMethod.Post, "/v1/moderation/warnings", CustomWebApplicationFactory.CreateModeratorUser());
            request.Content = JsonContent.Create(new
            {
                username = TestConstants.SecondUserLogin,
                entityType = "Profile",
                entityId = TestConstants.TestTopicId,
                points = 0,
                reason = "Неприемлемый статус"
            });

            var response = await Client.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Created, "body was: {0}", body);

            using var document = JsonDocument.Parse(body);
            var resource = document.RootElement.GetProperty("resource");
            warningId = resource.GetProperty("id").GetGuid();
            resource.GetProperty("entityType").GetString().Should().Be("Profile");
            resource.GetProperty("entityId").GetGuid().Should().Be(TestConstants.SecondUserId,
                "the object of a warning on a profile is the warned user, whatever the request named");
            var profile = resource.GetProperty("profile");
            profile.GetProperty("status").GetString().Should().Be("Статус перед выдачей");
            profile.GetProperty("info").GetString().Should().Contain("<em>О себе</em>");
            profile.GetProperty("picture").GetProperty("originalUrl").GetString().Should().Be(avatar.FilePath);

            await using var db = DatabaseFixture.CreateDbContext();
            var stored = await db.Warnings.SingleAsync(w => w.WarningId == warningId);
            stored.EntityId.Should().Be(TestConstants.SecondUserId);
            stored.EntityType.Should().Be(WarningEntityType.Profile);
            stored.EntitySnapshot.Should().Be("[i]О себе[/i] перед выдачей");
            stored.ProfileStatusSnapshot.Should().Be("Статус перед выдачей");
            stored.ProfileAvatarUploadId.Should().Be(uploadId);
        }
        finally
        {
            if (warningId.HasValue)
            {
                await Unseed(warningId.Value);
            }

            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.SecondUserId);
                (user.Status, user.Info, user.AvatarUploadId) = (statusBefore, infoBefore, avatarBefore);
                await db.SaveChangesAsync();
            }

            await UnseedUpload(uploadId);
        }
    }

    private Task<HttpResponseMessage> DeleteProfilePicture(Guid warningId, GeneralUser user) =>
        Client.SendAsync(CreateAuthenticatedRequest(
            HttpMethod.Delete, $"/v1/moderation/warnings/{warningId}/profile-picture", user));

    private async Task<DbUpload> StoredUpload(Guid uploadId)
    {
        await using var db = DatabaseFixture.CreateDbContext();
        return await db.Uploads.IgnoreQueryFilters().AsNoTracking().SingleAsync(u => u.UploadId == uploadId);
    }

    /// <summary>
    /// "Удалить картинку" in the moderation log: the picture a warning on a
    /// profile pinned leaves both logs at once, the status and "О себе" stay, and
    /// the upload carries the deletion of the moderator who asked, so the sweeper
    /// takes the file once the grace period counted from that moment is over.
    /// </summary>
    /// <remarks>
    /// Whatever its owner did to the avatar. Replaced or deleted by him, the row is
    /// behind the soft-delete filter, and "Все загруженные файлы" and
    /// DELETE /v1/uploads/{id} read through that filter and answer 404: before this
    /// endpoint moderation had no way at all to take a forbidden picture out of the
    /// open log once its owner changed his avatar.
    /// </remarks>
    [Theory]
    [InlineData("still the avatar")]
    [InlineData("replaced by its owner")]
    [InlineData("deleted by its owner")]
    public async Task TakeThePinnedPictureOutOfBothLogsForAModerator(string story)
    {
        var warningId = LogId(0x75);
        var uploadId = Guid.NewGuid();
        var avatar = story switch
        {
            "still the avatar" => Avatar(uploadId, TestConstants.SecondUserId, removed: false),
            "replaced by its owner" => Avatar(uploadId, TestConstants.SecondUserId),
            _ => Avatar(uploadId, TestConstants.SecondUserId, deletedBy: TestConstants.SecondUserId)
        };
        await SeedUpload(avatar);
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", "[b]О себе[/b]", uploadId));

        try
        {
            var response = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser());
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.NoContent, "body was: {0}", body);

            var stored = await StoredUpload(uploadId);
            stored.IsRemoved.Should().BeTrue("the picture is {0} and moderation deleted it", story);
            stored.DeletedByUserId.Should().Be(TestConstants.ModeratorUserId,
                "the deletion is the moderator's, which is what lets the sweeper take the pinned file");
            stored.DeletedUtc.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5),
                "the grace period starts when moderation deleted the picture, not when the owner replaced it");

            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            foreach (var (name, document) in new[] { ("open log", open), ("moderation", moderation) })
            {
                var profile = RowOf(document, warningId).GetProperty("profile");
                profile.TryGetProperty("picture", out _).Should().BeFalse("{0} no longer shows the picture", name);
                profile.GetProperty("status").GetString().Should().Be("Статус", "{0} keeps the status", name);
                profile.GetProperty("info").GetString().Should().Contain("<strong>О себе</strong>",
                    "{0} keeps О себе", name);
            }
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// A second press, by the same moderator or by another one who had the
    /// confirmation open at the same time, is answered with success and changes
    /// nothing: the author and the moment of the deletion stay the first one's.
    /// </summary>
    /// <remarks>
    /// One conditional statement rather than read, decide and write
    /// (DATA_STORAGE.md, "Атомарные встречные записи"): a deletion that is already
    /// moderation's is not overwritten, so the grace period is not restarted and the
    /// audit does not move to whoever pressed last. The picture is gone either way,
    /// which is all the person who pressed wanted, so he is not shown an error.
    /// </remarks>
    [Fact]
    public async Task AnswerASecondDeletionWithSuccessAndKeepTheFirstMark()
    {
        var warningId = LogId(0x76);
        var uploadId = Guid.NewGuid();
        await SeedUpload(Avatar(uploadId, TestConstants.SecondUserId));
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId));

        try
        {
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            var first = await StoredUpload(uploadId);

            (await DeleteProfilePicture(warningId, SeniorModerator()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent, "another moderator pressed it too");
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent, "the same moderator pressed it again");

            var after = await StoredUpload(uploadId);
            after.IsRemoved.Should().BeTrue();
            after.DeletedByUserId.Should().Be(TestConstants.ModeratorUserId, "the first deletion stays signed");
            after.DeletedUtc.Should().Be(first.DeletedUtc, "the grace period is not restarted");
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// A reader, a guest and the open log cannot take the picture out: the
    /// refusal is said, and the upload and the answer stay as they were.
    /// </summary>
    [Fact]
    public async Task RefuseTheDeletionOfAPinnedPictureToAReader()
    {
        var warningId = LogId(0x77);
        var uploadId = Guid.NewGuid();
        await SeedUpload(Avatar(uploadId, TestConstants.SecondUserId));
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, uploadId));

        try
        {
            var asReader = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateTestUser());
            asReader.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            var asGuest = await Client.DeleteAsync($"/v1/moderation/warnings/{warningId}/profile-picture");
            asGuest.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var stored = await StoredUpload(uploadId);
            stored.DeletedByUserId.Should().BeNull("the refusal must leave the replaced avatar as its owner left it");
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.SecondUserLogin}"));
            RowOf(open, warningId).GetProperty("profile").TryGetProperty("picture", out _)
                .Should().BeTrue("the picture stays in the log");
        }
        finally
        {
            await Unseed(warningId);
            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// A moderator does not take the picture out of a warning issued to himself,
    /// here by a higher rank, as one issued before his promotion: the refusal is
    /// said, nothing is marked, the picture stays in the log and his avatar in
    /// his profile.
    /// </summary>
    /// <remarks>
    /// A party does not rule on his own case, as with tickets. The picture is his
    /// own avatar, and the deletion used to be marked in his name: the log and
    /// the sweeper read such a mark as the owner's own doing and kept the picture,
    /// the answer was 204, "The warning shows no picture", and his current avatar
    /// left his profile.
    /// </remarks>
    [Fact]
    public async Task RefuseAModeratorThePictureOfAWarningIssuedToHimself()
    {
        var warningId = LogId(0x7a);
        var uploadId = Guid.NewGuid();
        var avatar = Avatar(uploadId, TestConstants.ModeratorUserId, removed: false);
        await SeedUpload(avatar);
        Guid? avatarBefore;
        await using (var db = DatabaseFixture.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.ModeratorUserId);
            avatarBefore = user.AvatarUploadId;
            user.AvatarUploadId = uploadId;
            await db.SaveChangesAsync();
        }

        var warning = OnProfile(warningId, TestConstants.ModeratorUserId, "Статус", null, uploadId);
        warning.AuthorId = TestConstants.SeniorModeratorUserId;
        await Seed(warning);

        try
        {
            var response = await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser());
            var body = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden, "body was: {0}", body);
            body.Should().Contain("Убрать картинку из записи лога о вас может только другой модератор");

            var stored = await StoredUpload(uploadId);
            stored.IsRemoved.Should().BeFalse("the refusal marks nothing");
            stored.DeletedByUserId.Should().BeNull();
            stored.DeletedUtc.Should().BeNull();

            using var profile = await ReadDocument(
                await Client.GetAsync($"/v1/users/{TestConstants.ModeratorUserLogin}"));
            profile.RootElement.GetProperty("resource").GetProperty("picture").GetProperty("originalUrl").GetString()
                .Should().Be(avatar.FilePath, "his avatar stays in his profile");
            using var open = await ReadDocument(
                await Client.GetAsync($"/v1/warnings?username={TestConstants.ModeratorUserLogin}"));
            RowOf(open, warningId).GetProperty("profile").TryGetProperty("picture", out _)
                .Should().BeTrue("the picture stays in the log");
        }
        finally
        {
            await Unseed(warningId);
            await using (var db = DatabaseFixture.CreateDbContext())
            {
                var user = await db.Users.SingleAsync(u => u.UserId == TestConstants.ModeratorUserId);
                user.AvatarUploadId = avatarBefore;
                await db.SaveChangesAsync();
            }

            await UnseedUpload(uploadId);
        }
    }

    /// <summary>
    /// A warning that pins no picture is answered with success: what was asked
    /// for already holds. A warning nobody issued is answered with 404, the code
    /// the change of points gives for it.
    /// </summary>
    [Fact]
    public async Task AnswerAWarningWithoutAPictureWithSuccessAndAnUnknownOneWithNotFound()
    {
        var warningId = LogId(0x78);
        await Seed(OnProfile(warningId, TestConstants.SecondUserId, "Статус", null, null));

        try
        {
            (await DeleteProfilePicture(warningId, CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await DeleteProfilePicture(LogId(0x79), CustomWebApplicationFactory.CreateModeratorUser()))
                .StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await Unseed(warningId);
        }
    }
}
