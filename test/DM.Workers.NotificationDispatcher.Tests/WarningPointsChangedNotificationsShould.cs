using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Workers.NotificationDispatcher.Notifiers.Moderation;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Who is told that the points of a warning moved, and what he is told.
/// </summary>
/// <remarks>
/// Addressed to the warned person alone, of three the row names; the payload carries both numbers, neither
/// derivable from the other.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class WarningPointsChangedNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public WarningPointsChangedNotificationsShould(NotificationDatabaseFixture fixture) =>
        _fixture = fixture;

    /// <summary>Issued by the master of the seed, corrected by the character's author.</summary>
    private static readonly Guid CorrectedWarningId =
        Guid.Parse("00000000-0000-0000-0000-0000000000f1");

    private const int PointsAtIssue = 3;
    private const int PointsNow = 1;
    private const string Reason = "Оскорбление в теме";
    private const string ChangeReason = "Нарушение оказалось легче";

    /// <summary>
    /// The corrected warning, written once; its three people are the ones the seed keeps apart.
    /// </summary>
    private async Task Seed()
    {
        await using var context = _fixture.CreateContext();
        if (await context.Warnings.AnyAsync(w => w.WarningId == CorrectedWarningId))
        {
            return;
        }

        context.Warnings.Add(new Warning
        {
            WarningId = CorrectedWarningId,
            TargetUserId = NotificationSeed.WaitedForUserId,
            AuthorId = NotificationSeed.MasterId,
            EntityId = Guid.Empty,
            EntityType = WarningEntityType.Unknown,
            CreatedUtc = DateTimeOffset.UtcNow.AddDays(-2),
            Text = Reason,
            Points = PointsNow,
            OriginalPoints = PointsAtIssue,
            PointsChangedByUserId = NotificationSeed.CharacterAuthorId,
            PointsChangedUtc = DateTimeOffset.UtcNow.AddDays(-1),
            PointsChangeText = ChangeReason
        });
        await context.SaveChangesAsync();
    }

    private async Task<CreateNotification?> Changed(Guid warningId)
    {
        await Seed();
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new WarningPointsChangedNotificationGenerator(context), warningId);
        return produced.SingleOrDefault();
    }

    [Fact]
    public async Task TellTheReaderTheWarningWasIssuedTo()
    {
        var notification = await Changed(CorrectedWarningId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(new[] { NotificationSeed.WaitedForUserId },
            "the correction is announced to the person carrying the points, and neither " +
            "the moderator who issued the warning nor the one who corrected it is him");
    }

    [Fact]
    public async Task SayWhatThePointsWereAndWhatTheyAre()
    {
        var notification = await Changed(CorrectedWarningId);

        notification.Should().NotBeNull();
        TicketSeed.Field(notification!, "OriginalPoints").Should().Be(PointsAtIssue,
            "a message that says only what the points are now never says what changed");
        TicketSeed.Field(notification!, "Points").Should().Be(PointsNow);
        TicketSeed.Field(notification!, "PointsChangeText").Should().Be(ChangeReason);
        TicketSeed.Field(notification!, "PointsChangedByUsername").Should().Be("seed-author",
            "the name shown is the moderator who corrected it, not the one who issued it");
        TicketSeed.Field(notification!, "ModeratorUsername").Should().Be(NotificationSeed.MasterUsername);
        TicketSeed.Field(notification!, "Username").Should().Be("seed-waited-for",
            "the way back into his own log is built out of this field alone");
        TicketSeed.Field(notification!, "Reason").Should().Be(Reason);
    }
}
