using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Features.Chats;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using FluentAssertions;
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
}
