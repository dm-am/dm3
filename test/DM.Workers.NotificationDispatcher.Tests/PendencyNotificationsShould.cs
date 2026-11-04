using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Notifiers.Game;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Who is told about a pendency, worked out from the rows.
/// </summary>
/// <remarks>
/// Three generators share one recipient rule over three projections: the user the pendency names, else the
/// character's author. The seed keeps the three people apart.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class PendencyNotificationsShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public PendencyNotificationsShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    private async Task<CreateNotification?> Reminder(Guid pendencyId)
    {
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new PostPendencyReminderNotificationGenerator(
                context, EveryNotificationGeneratorShould.ClockAt(DateTimeOffset.UtcNow)),
            pendencyId);
        return produced.SingleOrDefault();
    }

    private async Task<CreateNotification?> Created(Guid pendencyId)
    {
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new PostPendencyCreatedNotificationGenerator(context), pendencyId);
        return produced.SingleOrDefault();
    }

    private async Task<CreateNotification?> Fulfilled(Guid pendencyId)
    {
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new PostPendencyFulfilledNotificationGenerator(context), pendencyId);
        return produced.SingleOrDefault();
    }

    [Fact]
    public async Task RemindTheUserThePendencyNames()
    {
        var notification = await Reminder(NotificationSeed.PendencyWaitingForUserId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(new[] { NotificationSeed.WaitedForUserId },
            "the pendency names the person waited for, and neither the character's author " +
            "nor the master who wrote it down is that person");
    }

    [Fact]
    public async Task RemindTheCharactersAuthorWhenThePendencyNamesNobody()
    {
        var notification = await Reminder(NotificationSeed.PendencyWithoutWaitingUserId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(new[] { NotificationSeed.CharacterAuthorId },
            "with no user named, the turn belongs to whoever owns the character");
    }

    [Fact]
    public async Task SendNoReminderAboutAPendencyThatHasBeenAnswered()
    {
        var notification = await Reminder(NotificationSeed.FulfilledPendencyId);

        notification.Should().BeNull("the post it was waiting for has been written");
    }

    [Fact]
    public async Task CountTheDaysAPendencyHasBeenWaitingAndNameTheRoomItIsIn()
    {
        var notification = await Reminder(NotificationSeed.PendencyWaitingForUserId);

        notification.Should().NotBeNull();
        TicketSeed.Field(notification!, "DaysPending").Should().Be(NotificationSeed.DaysPending,
            "the reminder says how long, and the arithmetic is on the pendency's own timestamp");
        TicketSeed.Field(notification!, "RoomTitle").Should().Be(NotificationSeed.RoomTitle);
        TicketSeed.Field(notification!, "GameTitle").Should().Be(NotificationSeed.GameTitle);
        TicketSeed.Field(notification!, "CharacterName").Should().Be(NotificationSeed.CharacterName);
        TicketSeed.Field(notification!, "CreatedByUsername").Should().Be(NotificationSeed.MasterUsername);
    }

    [Fact]
    public async Task TellTheUserAPendencyNamesThatItWasCreated()
    {
        var notification = await Created(NotificationSeed.PendencyWaitingForUserId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(NotificationSeed.WaitedForUserId);
    }

    [Fact]
    public async Task TellTheCharactersAuthorAboutAPendencyThatNamesNobody()
    {
        var notification = await Created(NotificationSeed.PendencyWithoutWaitingUserId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(NotificationSeed.CharacterAuthorId);
    }

    [Fact]
    public async Task TellNobodyAboutAPendencyItsOwnAuthorIsWaitedFor()
    {
        var notification = await Created(NotificationSeed.PendencyWaitingForItsAuthorId);

        notification.Should().BeNull(
            "a master who writes down that he owes a post does not need to be told");
    }

    [Fact]
    public async Task TellThePendencysAuthorThatItWasFulfilled()
    {
        var notification = await Fulfilled(NotificationSeed.PendencyWaitingForUserId);

        notification.Should().NotBeNull();
        notification!.UsersInterested.Should().Equal(new[] { NotificationSeed.MasterId },
            "the person told is the one who was waiting for the post, not the one who wrote it");
        TicketSeed.Field(notification!, "FulfilledByUsername").Should().Be("seed-author",
            "the username shown is read off the character, which is what the post was written as");
    }

    [Fact]
    public async Task TellNobodyThatAPendencyWasFulfilledByTheOneWhoAskedForIt()
    {
        var notification = await Fulfilled(NotificationSeed.PendencyWaitingForItsAuthorId);

        notification.Should().BeNull();
    }
}
