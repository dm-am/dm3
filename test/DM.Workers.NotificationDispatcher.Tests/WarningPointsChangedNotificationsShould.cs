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
/// A warning is never withdrawn, so the correction of its points is the only
/// thing that happens to one after it is issued — and it happened in silence:
/// the person whose scale it changed found out by opening the log himself. The
/// notification is addressed to him and to nobody else, which is the half a
/// projection can get wrong without failing: the row names three people, and two
/// of them are moderators.
///
/// The numbers are the other half. Both of them are on the row and neither is
/// derivable from the other, so a payload carrying one says the points changed
/// and never says from what.
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
    /// The corrected warning, written once into the shared database. The three
    /// people it names are the three the seed keeps apart, so a generator
    /// answering the wrong one of them cannot pass.
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

    /// <summary>Reads a field off the anonymous metadata object.</summary>
    private static object? Field(CreateNotification notification, string name)
    {
        var property = notification.Metadata.GetType().GetProperty(name);
        property.Should().NotBeNull($"the metadata carries {name}");
        return property!.GetValue(notification.Metadata);
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
        Field(notification!, "OriginalPoints").Should().Be(PointsAtIssue,
            "a message that says only what the points are now never says what changed");
        Field(notification!, "Points").Should().Be(PointsNow);
        Field(notification!, "PointsChangeText").Should().Be(ChangeReason);
        Field(notification!, "PointsChangedByUsername").Should().Be("seed-author",
            "the name shown is the moderator who corrected it, not the one who issued it");
        Field(notification!, "ModeratorUsername").Should().Be(NotificationSeed.MasterUsername);
        Field(notification!, "Username").Should().Be("seed-waited-for",
            "the way back into his own log is built out of this field alone");
        Field(notification!, "Reason").Should().Be(Reason);
    }
}
