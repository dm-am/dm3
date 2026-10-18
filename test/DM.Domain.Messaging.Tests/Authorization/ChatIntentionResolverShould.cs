using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Domain.Messaging.Tests.Authorization;

public class ChatIntentionResolverShould
{
    private readonly ChatIntentionResolver _resolver = new();
    private readonly Guid _testUserId = Guid.NewGuid();

    private AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role
        };
    }

    [Fact]
    public void AllowAuthenticatedUserToCreateMessageInGlobalChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Global,
            Participants = Array.Empty<GeneralUser>()
        };

        var result = _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyGuestToCreateMessageInGlobalChat()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var chat = new Chat
        {
            Type = ChatType.Global,
            Participants = Array.Empty<GeneralUser>()
        };

        var result = _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowParticipantToCreateMessageInDirectChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Direct,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonParticipantToCreateMessageInDirectChat()
    {
        var user = CreateUser(_testUserId);
        var otherId = Guid.NewGuid();
        var chat = new Chat
        {
            Type = ChatType.Direct,
            Participants = new[] { new GeneralUser { UserId = otherId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowParticipantToCreateMessageInGroupChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Group,
            Participants = new[]
            {
                new GeneralUser { UserId = _testUserId },
                new GeneralUser { UserId = Guid.NewGuid() }
            }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowParticipantToUpdateGroupChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Group,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.UpdateChat, chat);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyUpdateDirectChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Direct,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.UpdateChat, chat);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyNonParticipantToUpdateGroupChat()
    {
        var user = CreateUser(_testUserId);
        var otherId = Guid.NewGuid();
        var chat = new Chat
        {
            Type = ChatType.Group,
            Participants = new[] { new GeneralUser { UserId = otherId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.UpdateChat, chat);

        result.Should().BeFalse();
    }

    // ═══ DELETE CHAT TESTS ═══

    [Fact]
    public void AllowParticipantToDeleteGroupChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Group,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.DeleteChat, chat);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyDeleteDirectChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Direct,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.DeleteChat, chat);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyNonParticipantToDeleteGroupChat()
    {
        var user = CreateUser(_testUserId);
        var otherId = Guid.NewGuid();
        var chat = new Chat
        {
            Type = ChatType.Group,
            Participants = new[] { new GeneralUser { UserId = otherId } }
        };

        var result = _resolver.IsAllowed(user, ChatIntention.DeleteChat, chat);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGlobalChatMessageUnderTheOrdinaryBan()
    {
        var user = new AuthenticatedUser
        {
            UserId = _testUserId,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.DemocraticBan
        };
        var chat = new Chat { Type = ChatType.Global, Participants = Array.Empty<GeneralUser>() };

        _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat).Should().BeFalse();
    }

    [Fact]
    public void StillAllowDirectMessagesUnderTheOrdinaryBan()
    {
        var user = new AuthenticatedUser
        {
            UserId = _testUserId,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.DemocraticBan
        };
        var chat = new Chat
        {
            Type = ChatType.Direct,
            Participants = new[] { new GeneralUser { UserId = _testUserId } }
        };

        // Private correspondence is outside the ordinary ban by design
        _resolver.IsAllowed(user, ChatIntention.CreateMessage, chat).Should().BeTrue();
    }

    [Fact]
    public void DenyDeleteGlobalChat()
    {
        var user = CreateUser(_testUserId);
        var chat = new Chat
        {
            Type = ChatType.Global,
            Participants = Array.Empty<GeneralUser>()
        };

        var result = _resolver.IsAllowed(user, ChatIntention.DeleteChat, chat);

        result.Should().BeFalse();
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
        var user = CreateUser(_testUserId);

        _resolver.IsAllowed(user, ChatIntention.LeaveChat, ChatOf(type, participates)).Should().Be(expected);
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
        var user = CreateUser(_testUserId);

        _resolver.IsAllowed(user, ChatIntention.ClearHistory, ChatOf(type, participates)).Should().Be(expected);
    }

    private Chat ChatOf(ChatType type, bool participates) => new()
    {
        Type = type,
        Participants = participates
            ? new[] { new GeneralUser { UserId = _testUserId }, new GeneralUser { UserId = Guid.NewGuid() } }
            : new[] { new GeneralUser { UserId = Guid.NewGuid() } }
    };
}
