using System;
using System.Threading.Tasks;
using DM.Workers.NotificationDispatcher.Notifiers;
using DM.Workers.NotificationDispatcher.Notifiers.Game;
using DM.Workers.NotificationDispatcher.Notifiers.Security;
using AwesomeAssertions;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// A notification is timed by the clock the container hands the generator.
/// </summary>
/// <remarks>The moment it happened is the value a reader of a security letter judges, so it comes from IDateTimeProvider.</remarks>
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
            TicketSeed.Field(produced[0], "EventTime").Should().Be(Moment.UtcDateTime,
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
            new PostPendencyReminderNotificationGenerator(context, tenDaysOn),
            NotificationSeed.PendencyWaitingForUserId);

        produced.Should().ContainSingle();
        TicketSeed.Field(produced[0], "DaysPending").Should().Be(NotificationSeed.DaysPending + 10,
            "the count runs from the pendency's own timestamp to the clock the generator " +
            "is given, and ten days on that clock are ten days more waiting");
    }

}
