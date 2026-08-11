using System;
using System.Threading.Tasks;
using DM.Domain.Personal.Features.Notifications;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers.Game;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers.Security;
using FluentAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A notification is timed by the clock the container hands the generator.
/// </summary>
/// <remarks>
/// Four generators read the machine clock through a static: the three security
/// letters stamped their EventTime with DateTime.UtcNow, and the pendency
/// reminder counted its days off DateTimeOffset.UtcNow. IDateTimeProvider is the
/// system abstraction of the kernel that every domain service already takes, and
/// this worker was the one host that asked for it nowhere - so the single value a
/// reader of a security letter is asked to judge, the moment it happened, was
/// whatever the machine said and could not be asserted at all.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class NotificationClockShould
{
    /// <summary>Any moment, as long as it is not the one the machine reads.</summary>
    private static readonly DateTimeOffset Moment = new(2026, 5, 17, 9, 45, 13, TimeSpan.Zero);

    private readonly NotificationDatabaseFixture _fixture;

    public NotificationClockShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task StampASecurityLetterWithTheMomentTheClockGives()
    {
        await using var context = _fixture.CreateContext();
        var clock = EveryNotificationGeneratorShould.ClockAt(Moment);

        var generators = new INotificationGenerator[]
        {
            new PasswordChangedNotificationGenerator(context, clock),
            new EmailChangedNotificationGenerator(context, clock),
            new AccountLockedNotificationGenerator(context, clock)
        };

        foreach (var generator in generators)
        {
            var produced = await EveryNotificationGeneratorShould.DrainAsync(
                generator, NotificationSeed.MasterId);

            produced.Should().ContainSingle(
                "{0} answers about a user the seed has", generator.GetType().Name);
            Field(produced[0], "EventTime").Should().Be(Moment.UtcDateTime,
                "the letter tells the reader when it happened, and the moment has to be " +
                "the one the host's clock was asked for");
        }
    }

    [Fact]
    public async Task CountThePendencysDaysAgainstTheSameClock()
    {
        await using var context = _fixture.CreateContext();
        var tenDaysOn = EveryNotificationGeneratorShould.ClockAt(DateTimeOffset.UtcNow.AddDays(10));

        var produced = await EveryNotificationGeneratorShould.DrainAsync(
            new GamePendencyReminderNotificationGenerator(context, tenDaysOn),
            NotificationSeed.PendencyWaitingForUserId);

        produced.Should().ContainSingle();
        Field(produced[0], "DaysPending").Should().Be(NotificationSeed.DaysPending + 10,
            "the count runs from the pendency's own timestamp to the clock the generator " +
            "is given, and ten days on that clock are ten days more waiting");
    }

    /// <summary>Reads a field off the anonymous metadata object.</summary>
    private static object? Field(CreateNotification notification, string name)
    {
        var property = notification.Metadata.GetType().GetProperty(name);
        property.Should().NotBeNull($"the metadata carries {name}");
        return property!.GetValue(notification.Metadata);
    }
}
