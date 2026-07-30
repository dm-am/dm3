using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.GlobalChatEvents;
using DM.Testing.Dsl;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Messaging.Tests.Authorization;

/// <summary>
/// Gates a site-wide chat event. An event is visible to everyone on the site, so
/// the costly mistake is letting somebody who is not running it rename, retime or
/// delete it out from under the people who are.
/// </summary>
public class GlobalChatEventIntentionResolverShould
{
    private readonly GlobalChatEventIntentionResolver resolver = new();

    private static readonly Guid OrganizerId = Guid.NewGuid();
    private static readonly Guid ParticipantId = Guid.NewGuid();
    private static readonly Guid StrangerId = Guid.NewGuid();

    private static GlobalChatEvent EventWith(
        GlobalChatEventStatus status = GlobalChatEventStatus.Scheduled,
        bool isOpen = true)
    {
        return new GlobalChatEvent
        {
            Id = Guid.NewGuid(),
            Title = "Test Event",
            Description = "Test Description",
            Status = status,
            IsOpen = isOpen,
            CreatedBy = new GeneralUser { UserId = OrganizerId },
            Participants = new List<GlobalChatEventParticipant>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    User = new GeneralUser { UserId = OrganizerId },
                    IsOrganizer = true
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    User = new GeneralUser { UserId = ParticipantId },
                    IsOrganizer = false
                }
            }
        };
    }

    #region Creating

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Admin)]
    public void LetSeniorModerationCreateAnEvent(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Create).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Mentor)]
    [InlineData(UserRole.Moderator)]
    public void NotLetAnyoneBelowSeniorModerationCreateAnEvent(UserRole role)
    {
        var user = Create.User().WithRole(role).Please();

        // An event takes over the global chat for everybody on the site.
        resolver.IsAllowed(user, GlobalChatEventIntention.Create).Should().BeFalse();
    }

    [Fact]
    public void NotLetAGuestCreateAnEvent()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, GlobalChatEventIntention.Create).Should().BeFalse();
    }

    [Theory]
    [InlineData(GlobalChatEventIntention.Update)]
    [InlineData(GlobalChatEventIntention.Delete)]
    [InlineData(GlobalChatEventIntention.Join)]
    [InlineData(GlobalChatEventIntention.Leave)]
    public void RefuseTargetedIntentionsAskedWithoutAnEvent(GlobalChatEventIntention intention)
    {
        var user = Create.User().WithRole(UserRole.Admin).Please();

        // The targetless overload answers Create and nothing else, so an
        // intention that needs an event cannot slip past by omitting it.
        resolver.IsAllowed(user, intention).Should().BeFalse();
    }

    #endregion

    #region Running the event

    [Theory]
    [InlineData(GlobalChatEventIntention.Update)]
    [InlineData(GlobalChatEventIntention.Delete)]
    [InlineData(GlobalChatEventIntention.AddParticipant)]
    [InlineData(GlobalChatEventIntention.RemoveParticipant)]
    public void LetAnOrganizerRunTheEvent(GlobalChatEventIntention intention)
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();

        resolver.IsAllowed(user, intention, EventWith()).Should().BeTrue();
    }

    [Theory]
    [InlineData(GlobalChatEventIntention.Update)]
    [InlineData(GlobalChatEventIntention.Delete)]
    [InlineData(GlobalChatEventIntention.AddParticipant)]
    [InlineData(GlobalChatEventIntention.RemoveParticipant)]
    public void NotLetAPlainParticipantRunTheEvent(GlobalChatEventIntention intention)
    {
        var user = Create.User(ParticipantId).WithRole(UserRole.RegularUser).Please();

        // Attending is not organizing: the participant row carries the flag that
        // separates the two.
        resolver.IsAllowed(user, intention, EventWith()).Should().BeFalse();
    }

    [Theory]
    [InlineData(GlobalChatEventIntention.Update)]
    [InlineData(GlobalChatEventIntention.Delete)]
    public void NotLetAnOutsiderTakeOverTheEvent(GlobalChatEventIntention intention)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.Admin).Please();

        // Site rank opens the door to creating events, not to seizing one that
        // somebody else is already running.
        resolver.IsAllowed(user, intention, EventWith()).Should().BeFalse();
    }

    #endregion

    #region Lifecycle

    [Fact]
    public void LetAnOrganizerStartAScheduledEvent()
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Start, EventWith()).Should().BeTrue();
    }

    [Theory]
    [InlineData(GlobalChatEventStatus.Live)]
    [InlineData(GlobalChatEventStatus.Ended)]
    public void NotRestartAnEventThatIsNoLongerScheduled(GlobalChatEventStatus status)
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Start, EventWith(status)).Should().BeFalse();
    }

    [Fact]
    public void LetAnOrganizerEndALiveEvent()
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();
        var chatEvent = EventWith(GlobalChatEventStatus.Live);

        resolver.IsAllowed(user, GlobalChatEventIntention.End, chatEvent).Should().BeTrue();
    }

    [Theory]
    [InlineData(GlobalChatEventStatus.Scheduled)]
    [InlineData(GlobalChatEventStatus.Ended)]
    public void NotEndAnEventThatIsNotLive(GlobalChatEventStatus status)
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.End, EventWith(status)).Should().BeFalse();
    }

    [Theory]
    [InlineData(GlobalChatEventIntention.Start)]
    [InlineData(GlobalChatEventIntention.End)]
    public void NotLetAParticipantDriveTheLifecycle(GlobalChatEventIntention intention)
    {
        var user = Create.User(ParticipantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, EventWith(GlobalChatEventStatus.Live)).Should().BeFalse();
        resolver.IsAllowed(user, intention, EventWith()).Should().BeFalse();
    }

    #endregion

    #region Joining and leaving

    [Theory]
    [InlineData(GlobalChatEventStatus.Scheduled)]
    [InlineData(GlobalChatEventStatus.Live)]
    public void LetAnAuthenticatedUserJoinAnOpenEvent(GlobalChatEventStatus status)
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Join, EventWith(status)).Should().BeTrue();
    }

    [Fact]
    public void NotLetAnyoneJoinAClosedEvent()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var chatEvent = EventWith(isOpen: false);

        // A closed event has a guest list, and the organizer writes it.
        resolver.IsAllowed(user, GlobalChatEventIntention.Join, chatEvent).Should().BeFalse();
    }

    [Fact]
    public void NotLetAnyoneJoinAnEndedEvent()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();
        var chatEvent = EventWith(GlobalChatEventStatus.Ended);

        resolver.IsAllowed(user, GlobalChatEventIntention.Join, chatEvent).Should().BeFalse();
    }

    [Fact]
    public void NotLetAGuestJoin()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, GlobalChatEventIntention.Join, EventWith())
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NotLetSomebodyAlreadyOnTheRosterJoinAgain(bool asOrganizer)
    {
        var user = Create.User(asOrganizer ? OrganizerId : ParticipantId)
            .WithRole(UserRole.RegularUser)
            .Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Join, EventWith()).Should().BeFalse();
    }

    [Fact]
    public void LetAParticipantLeave()
    {
        var user = Create.User(ParticipantId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Leave, EventWith()).Should().BeTrue();
    }

    [Fact]
    public void NotLetAnOrganizerLeaveTheirOwnEvent()
    {
        var user = Create.User(OrganizerId).WithRole(UserRole.SeniorModerator).Please();

        // Walking out would leave the event with nobody able to start or end it.
        resolver.IsAllowed(user, GlobalChatEventIntention.Leave, EventWith()).Should().BeFalse();
    }

    [Fact]
    public void NotLetAnOutsiderLeave()
    {
        var user = Create.User(StrangerId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, GlobalChatEventIntention.Leave, EventWith()).Should().BeFalse();
    }

    #endregion

    #region Behaviour as found

    [Theory]
    [InlineData(AccessPolicy.DemocraticBan)]
    [InlineData(AccessPolicy.FullBan)]
    public void LetABannedUserPutTheirNameOnAPublicEventRoster(AccessPolicy policy)
    {
        var user = Create.User(StrangerId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(policy)
            .Please();

        // Behaviour as found, not a rule that was chosen. Join checks
        // IsAuthenticated and never MaySpeak, so a banned user joins and appears
        // in the event's public participant list. Speech itself is still stopped
        // one layer on — ChatIntentionResolver gates CreateMessage in the global
        // chat with MaySpeak — so this leaks presence, not messages. Whether the
        // roster counts as public speech is the decision to make.
        resolver.IsAllowed(user, GlobalChatEventIntention.Join, EventWith()).Should().BeTrue();
    }

    #endregion
}
