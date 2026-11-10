using DM.Domain.Core.Chats;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbCommentEdit = DM.Infrastructure.Persistence.Entities.Shared.CommentEdit;
using DbMessage = DM.Infrastructure.Persistence.Entities.Messaging.Message;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

public class WarningOpenLogShould : WarningOpenLogTestBase
{
    public WarningOpenLogShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    // A copy of a text with no place the server can name is kept out of it: whether a guest could
    // read the text where it stood has no answer.
    [Fact]
    public async Task KeepAPlacelessTextOutOfTheOpenLog()
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

            // The fields of a correction, of the place and of the text are absent
            // rather than null: the serialiser drops nulls.
            row.EnumerateObject().Select(field => field.Name).Should().BeEquivalentTo(
                ["id", "user", "moderator", "points", "originalPoints", "reason", "createdUtc"],
                "a row whose text cannot be placed shows neither the text nor anything about a place");

            using var moderation = await ReadDocument(await Client.SendAsync(CreateAuthenticatedRequest(
                HttpMethod.Get, $"/v1/moderation/warnings?username={TestConstants.SecondUserLogin}",
                CustomWebApplicationFactory.CreateModeratorUser())));
            RowOf(moderation, shown).GetProperty("evidence").GetString().Should().Contain("Снимок оскорбления");
        }
        finally
        {
            await Unseed(shown);
        }
    }

    // The snapshot is the source, which may carry a hidden block; the log gets the rendering.
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
    /// What moderation took off the site and what its author deleted are both
    /// shown, as the text the warning was given for, with the place and the date
    /// of the text and without an address to follow.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ShowADeletedTextInTheOpenLogWithoutAnAddress(bool deletedByModeration)
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

    // The snapshot never changes, so the edit history of the object is not read.
    [Fact]
    public async Task ShowTheWarnedTextAfterAnEditWithoutSayingItWasEdited()
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
            ChatId = WellKnownChats.GlobalChatId,
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

    // Nothing is erased: a reader tells a warning found unjustified from one never issued.
    [Fact]
    public async Task KeepACorrectedWarningInTheOpenLogWithBothSignatures()
    {
        var corrected = LogId(0x51);
        var changedUtc = new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero);
        await Seed(Row(corrected, DateTimeOffset.UtcNow.AddMinutes(-1),
            text: "Спойлер без тега в глобальном чате", points: 0, originalPoints: 2,
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
            row.GetProperty("reason").GetString().Should().Be("Спойлер без тега в глобальном чате");
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

    // By the rows on the page: the envelope rounds its echo of skip.
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

    // Ordered by the moment and the identifier: the DM2 import carries equal timestamps.
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
}
