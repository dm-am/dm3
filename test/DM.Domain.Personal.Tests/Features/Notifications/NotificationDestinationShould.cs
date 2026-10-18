using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

/// <summary>
/// A notification about something that has a page leads to that page, and one
/// about the account itself leads nowhere.
/// </summary>
/// <remarks>
/// A notification without a path is a line nobody can click, and the list in the
/// application marks a line read by the click on it, so a notification that could
/// have led somewhere and does not stays bold until the reader clears the whole
/// list. The paths are asserted as the bag arrives from the generator.
/// </remarks>
public class NotificationDestinationShould
{
    [Fact]
    public void LeadAGrantedAwardToTheHallOfFameOfTheRecipient()
    {
        var granted = new
        {
            UserAwardId = "4c1b2a3d-0000-0000-0000-000000000001",
            Username = "Player-Two",
            AwardTitle = "Народное признание, например",
            GrantedByUsername = "SolohinLex"
        };

        NotificationDestination.GetPath(EventType.AwardGranted, granted)
            .Should().Be("/users/Player-Two?tab=achievements",
                "the award stands on the hall of fame tab of the profile it was granted to, " +
                "and the name of the one who granted it is not the address");
    }

    [Fact]
    public void EscapeTheNameOfTheRecipient() =>
        NotificationDestination.GetPath(EventType.AwardGranted, new { Username = "Игрок Два" })
            .Should().Be("/users/%D0%98%D0%B3%D1%80%D0%BE%D0%BA%20%D0%94%D0%B2%D0%B0?tab=achievements");

    [Fact]
    public void LeadAFiledTicketToThePageModerationTakesItUpOn()
    {
        var filed = new
        {
            TicketId = "2f1c9a8e-4b7d-4e3a-9c1f-0a2b3c4d5e6f",
            Description = "Текст обращения",
            AuthorUsername = "Player-Two"
        };

        NotificationDestination.GetPath(EventType.TicketCreated, filed)
            .Should().Be("/moderation/tickets/2f1c9a8e-4b7d-4e3a-9c1f-0a2b3c4d5e6f",
                "a filed ticket is told to the moderators, and they answer it on its page");
    }

    /// <summary>
    /// What happened to the account itself has no page to show it: a link would
    /// lead to the front page, which is a promise the notification does not keep.
    /// </summary>
    [Theory]
    [InlineData(EventType.PasswordChanged)]
    [InlineData(EventType.EmailChanged)]
    [InlineData(EventType.SuspiciousLoginActivity)]
    [InlineData(EventType.AccountLocked)]
    public void LeadAnAccountEventNowhere(EventType eventType) =>
        NotificationDestination.GetPath(eventType, new { Username = "Player-Two", EventTime = "2026-10-14" })
            .Should().BeNull();
}
