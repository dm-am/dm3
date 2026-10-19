using System;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Messaging.Tests.Authorization;

public class ChatIntentionResolverShould
{
    private readonly ChatIntentionResolver resolver = new();
    private readonly Guid testUserId = Guid.NewGuid();

    [Theory]
    [InlineData(ChatIntention.CreateMessage, ChatType.Global, false, true)]
    [InlineData(ChatIntention.CreateMessage, ChatType.Direct, true, true)]
    [InlineData(ChatIntention.CreateMessage, ChatType.Direct, false, false)]
    [InlineData(ChatIntention.CreateMessage, ChatType.Group, true, true)]
    [InlineData(ChatIntention.UpdateChat, ChatType.Group, true, true)]
    [InlineData(ChatIntention.UpdateChat, ChatType.Group, false, false)]
    [InlineData(ChatIntention.UpdateChat, ChatType.Direct, true, false)]
    [InlineData(ChatIntention.DeleteChat, ChatType.Group, true, true)]
    [InlineData(ChatIntention.DeleteChat, ChatType.Group, false, false)]
    [InlineData(ChatIntention.DeleteChat, ChatType.Direct, true, false)]
    [InlineData(ChatIntention.DeleteChat, ChatType.Global, false, false)]
    public void AnswerByTheChatAndWhetherTheCallerTakesPart(
        ChatIntention intention, ChatType type, bool participates, bool expected)
    {
        var user = Create.User(testUserId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, intention, ChatOf(type, participates)).Should().Be(expected);
    }

    [Fact]
    public void DenyGuestToCreateMessageInGlobalChat()
    {
        resolver.IsAllowed(AuthenticatedUser.Guest, ChatIntention.CreateMessage, ChatOf(ChatType.Global, false))
            .Should().BeFalse();
    }

    /// <summary>
    /// The ordinary ban silences the global chat; private correspondence is outside it by design.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Global, false, false)]
    [InlineData(ChatType.Direct, true, true)]
    public void SilenceOnlyTheGlobalChatUnderTheOrdinaryBan(ChatType type, bool participates, bool expected)
    {
        var user = Create.User(testUserId)
            .WithRole(UserRole.RegularUser)
            .WithAccessPolicy(AccessPolicy.DemocraticBan)
            .Please();

        resolver.IsAllowed(user, ChatIntention.CreateMessage, ChatOf(type, participates)).Should().Be(expected);
    }

    /// <summary>
    /// A group is left by its participant; a direct chat is not left at all, and
    /// a stranger has nothing to leave. The global chat and a game room chat
    /// carry no participant rows.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Group, true, true)]
    [InlineData(ChatType.Group, false, false)]
    [InlineData(ChatType.Direct, true, false)]
    [InlineData(ChatType.Global, false, false)]
    [InlineData(ChatType.GameRoom, false, false)]
    public void AllowLeavingAGroupToItsParticipantOnly(ChatType type, bool participates, bool expected)
    {
        var user = Create.User(testUserId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, ChatIntention.LeaveChat, ChatOf(type, participates)).Should().Be(expected);
    }

    /// <summary>
    /// The history is cleared by a participant of a direct or group chat, for
    /// himself. A stranger has no history there to clear, and the global chat
    /// and a game room chat have no participant row to keep the mark on.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Group, true, true)]
    [InlineData(ChatType.Direct, true, true)]
    [InlineData(ChatType.Group, false, false)]
    [InlineData(ChatType.Direct, false, false)]
    [InlineData(ChatType.Global, false, false)]
    [InlineData(ChatType.GameRoom, false, false)]
    public void AllowClearingTheHistoryToAParticipantOfAConversation(ChatType type, bool participates, bool expected)
    {
        var user = Create.User(testUserId).WithRole(UserRole.RegularUser).Please();

        resolver.IsAllowed(user, ChatIntention.ClearHistory, ChatOf(type, participates)).Should().Be(expected);
    }

    private Chat ChatOf(ChatType type, bool participates) => new()
    {
        Type = type,
        Participants = participates
            ? new[] { new GeneralUser { UserId = testUserId }, new GeneralUser { UserId = Guid.NewGuid() } }
            : new[] { new GeneralUser { UserId = Guid.NewGuid() } }
    };
}
