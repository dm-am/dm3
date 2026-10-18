using System;
using System.Linq;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

/// <summary>
/// The list in the application shows what a letter prints, less what the row
/// already says.
/// </summary>
/// <remarks>
/// A letter has nothing beside it, so it prints the time it is about. A row of the
/// list carries its own date next to the title, and the same time printed again
/// underneath would be the one field the reader sees twice. The other times in a
/// bag are moments of their own, the end of a ban or the day an award was handed
/// out, and those are formatted on the reader's clock like every date of the site,
/// so they are marked rather than formatted here.
/// </remarks>
public class NotificationDetailsShould
{
    private static readonly DateTimeOffset Moment = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LeaveOutTheTimeTheRowAlreadyShows()
    {
        var details = NotificationWording.ReadDetails(new
        {
            Reason = "Флуд",
            CreatedUtc = Moment,
            EventTime = Moment
        });

        details.Should().Equal(new NotificationDetail("Причина", "Флуд", false));
    }

    [Fact]
    public void MarkTheMomentsForTheReadersClock()
    {
        var details = NotificationWording.ReadDetails(new
        {
            StartedUtc = Moment,
            EndedUtc = Moment,
            AwardedUtc = Moment,
            Points = 3
        });

        details.Where(detail => detail.IsMoment).Select(detail => detail.Label)
            .Should().Equal("Начало", "Окончание", "Вручено");
        details.Where(detail => detail.IsMoment).Select(detail => DateTimeOffset.Parse(detail.Value))
            .Should().AllBeEquivalentTo(Moment, "the moment travels as written, for the client to format");
        details.Single(detail => !detail.IsMoment)
            .Should().Be(new NotificationDetail("Баллы", "3", false));
    }

    [Fact]
    public void ShowNothingTheLetterHides()
    {
        var details = NotificationWording.ReadDetails(new
        {
            Username = "Player-Two",
            GameId = "vedmak~aBcD-1_2",
            GameTitle = "",
            ModeratorUsername = "TestSeniorMod"
        });

        details.Should().Equal(new NotificationDetail("Модератор", "TestSeniorMod", false));
    }

    [Fact]
    public void ReadNothingOutOfABagThatIsNotAnObject() =>
        NotificationWording.ReadDetails("not a bag").Should().BeEmpty(
            "a bag that cannot be read costs the notification its details, not its title");
}
