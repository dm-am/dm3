using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Personal.Tests.Features.Notifications;

// The list marks a line read by the click on it, so a line with no path stays unread.
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

    private static readonly Dictionary<string, string> EveryIdentifier = new()
    {
        ["PublicationId"] = "p",
        ["BlogId"] = "b",
        ["TopicId"] = "t",
        ["GameId"] = "g",
        ["Username"] = "u",
        ["TicketId"] = "k"
    };

    private static readonly Dictionary<EventType, string> Paths = new()
    {
        [EventType.NewPublication] = "/publication/p",
        [EventType.ChangedPublication] = "/publication/p",
        [EventType.LikedPublication] = "/publication/p",
        [EventType.LikedPublicationComment] = "/publication/p",
        [EventType.NewPublicationComment] = "/publication/p",
        [EventType.NewBlogComment] = "/blogs/b",
        [EventType.LikedBlogComment] = "/blogs/b",
        [EventType.BlogInvitationCreated] = "/blogs/b",
        [EventType.StatusBlogActive] = "/blogs/b",
        [EventType.StatusBlogClosed] = "/blogs/b",
        [EventType.StatusBlogFrozen] = "/blogs/b",
        [EventType.StatusBlogFinished] = "/blogs/b",
        [EventType.NewBlogFromSubscribedAuthor] = "/blogs/b",
        [EventType.ChangedTopic] = "/forum-topic/t",
        [EventType.LikedTopic] = "/forum-topic/t",
        [EventType.LikedTopicComment] = "/forum-topic/t",
        [EventType.NewCommentInSubscribedTopic] = "/forum-topic/t",
        [EventType.NewTopicFromSubscribedAuthor] = "/forum-topic/t",
        [EventType.NewCharacter] = "/game/g/characters",
        [EventType.WarningIssued] = "/warnings?username=u",
        [EventType.WarningPointsChanged] = "/warnings?username=u",
        [EventType.BanIssued] = "/users/u",
        [EventType.TicketAnswered] = "/my-tickets/k",
        [EventType.TicketResolved] = "/my-tickets/k",
        [EventType.TicketReplied] = "/moderation/tickets/k",
        [EventType.TicketCreated] = "/moderation/tickets/k",
        [EventType.AwardGranted] = "/users/u?tab=achievements",
        [EventType.StatusGameActive] = "/game/g",
        [EventType.StatusGameClosed] = "/game/g",
        [EventType.StatusGameFrozen] = "/game/g",
        [EventType.StatusGameFinished] = "/game/g",
        [EventType.GameClosureWarning] = "/game/g",
        [EventType.GameInactivityWarning] = "/game/g",
        [EventType.GameRecruitmentOpened] = "/game/g",
        [EventType.LikedGameComment] = "/game/g",
        [EventType.AssignmentRequestCreated] = "/game/g",
        [EventType.PlayerInvitationCreated] = "/game/g",
        [EventType.ReaderInvitationCreated] = "/game/g",
        [EventType.RoomPendencyCreated] = "/game/g",
        [EventType.RoomPendencyFulfilled] = "/game/g",
        [EventType.RoomPendencyReminder] = "/game/g",
        [EventType.StatusCharacterAccepted] = "/game/g",
        [EventType.StatusCharacterDeclined] = "/game/g",
        [EventType.StatusCharacterDied] = "/game/g",
        [EventType.StatusCharacterResurrected] = "/game/g",
        [EventType.StatusCharacterReadmitted] = "/game/g",
        [EventType.StatusCharacterLeft] = "/game/g",
        [EventType.StatusCharacterExiled] = "/game/g",
        [EventType.NewGameFromSubscribedAuthor] = "/game/g",
    };

    [Fact]
    public void LeadEveryEventWhereTheTableSays()
    {
        foreach (var eventType in Enum.GetValues<EventType>())
        {
            NotificationDestination.GetPath(eventType, EveryIdentifier)
                .Should().Be(Paths.GetValueOrDefault(eventType), "the path of {0}", eventType);
        }
    }
}
