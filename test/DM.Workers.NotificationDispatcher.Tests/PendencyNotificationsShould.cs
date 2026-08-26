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
/// Three generators share one recipient rule — the user the pendency names, and
/// the character's author when it names nobody — and each of the three reads it
/// off a different projection. Nothing executed any of them: a projection that
/// stopped selecting the character's author would compile, would answer null, and
/// would take the worker down on the dereference; a rule that picked the wrong one
/// of the three people involved would tell the wrong person to write a post. The
/// seed keeps those three people apart so that neither can pass for the other.
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
            new GamePendencyReminderNotificationGenerator(
                context, EveryNotificationGeneratorShould.ClockAt(DateTimeOffset.UtcNow)),
            pendencyId);
        return produced.SingleOrDefault();
    }

    private async Task<CreateNotification?> Created(Guid pendencyId)
    {
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new GamePendencyCreatedNotificationGenerator(context), pendencyId);
        return produced.SingleOrDefault();
    }

    private async Task<CreateNotification?> Fulfilled(Guid pendencyId)
    {
        await using var context = _fixture.CreateContext();
        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new GamePendencyFulfilledNotificationGenerator(context), pendencyId);
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
        Field(notification!, "DaysPending").Should().Be(NotificationSeed.DaysPending,
            "the reminder says how long, and the arithmetic is on the pendency's own timestamp");
        Field(notification!, "RoomTitle").Should().Be(NotificationSeed.RoomTitle);
        Field(notification!, "GameTitle").Should().Be(NotificationSeed.GameTitle);
        Field(notification!, "CharacterName").Should().Be(NotificationSeed.CharacterName);
        Field(notification!, "CreatedByUsername").Should().Be(NotificationSeed.MasterUsername);
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
        Field(notification!, "FulfilledByUsername").Should().Be("seed-author",
            "the username shown is read off the character, which is what the post was written as");
    }

    [Fact]
    public async Task TellNobodyThatAPendencyWasFulfilledByTheOneWhoAskedForIt()
    {
        var notification = await Fulfilled(NotificationSeed.PendencyWaitingForItsAuthorId);

        notification.Should().BeNull();
    }
}
