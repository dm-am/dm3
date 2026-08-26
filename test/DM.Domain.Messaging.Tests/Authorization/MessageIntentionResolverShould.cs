using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Authorization;

public class MessageIntentionResolverShould : UnitTestBase
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly MessageIntentionResolver _resolver;
    private readonly Guid _testUserId = Guid.NewGuid();
    private readonly DateTime _now = DateTime.UtcNow;

    public MessageIntentionResolverShould()
    {
        _dateTimeProvider = Mock<IDateTimeProvider>();
        _dateTimeProvider.Now.Returns(_now);

        var options = Options.Create(new MessagingConfiguration
        {
            EditTimeoutMinutes = 60
        });

        _resolver = new MessageIntentionResolver(options, _dateTimeProvider);
    }

    private AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser)
    {
        return new AuthenticatedUser
        {
            UserId = userId,
            Role = role
        };
    }

    private Message CreateMessage(Guid authorId, DateTime createdUtc)
    {
        return new Message
        {
            Author = new GeneralUser { UserId = authorId },
            CreatedUtc = createdUtc
        };
    }

    [Fact]
    public void AllowAuthorToEditWithinTimeLimit()
    {
        var user = CreateUser(_testUserId);
        var message = CreateMessage(_testUserId, _now.AddMinutes(-30));

        var result = _resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyAuthorToEditAfterTimeLimit()
    {
        var user = CreateUser(_testUserId);
        var message = CreateMessage(_testUserId, _now.AddMinutes(-61));

        var result = _resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowAuthorToDeleteWithinTimeLimit()
    {
        var user = CreateUser(_testUserId);
        var message = CreateMessage(_testUserId, _now.AddMinutes(-30));

        var result = _resolver.IsAllowed(user, MessageIntention.Delete, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyAuthorToDeleteAfterTimeLimit()
    {
        var user = CreateUser(_testUserId);
        var message = CreateMessage(_testUserId, _now.AddMinutes(-61));

        var result = _resolver.IsAllowed(user, MessageIntention.Delete, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowModeratorToEditAnyMessage()
    {
        var moderatorId = Guid.NewGuid();
        var user = CreateUser(moderatorId, UserRole.Moderator);
        var message = CreateMessage(_testUserId, _now.AddDays(-10));

        var result = _resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAdminToDeleteAnyMessage()
    {
        var adminId = Guid.NewGuid();
        var user = CreateUser(adminId, UserRole.Admin);
        var message = CreateMessage(_testUserId, _now.AddDays(-10));

        var result = _resolver.IsAllowed(user, MessageIntention.Delete, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyNonAuthorToEditMessage()
    {
        var otherUserId = Guid.NewGuid();
        var user = CreateUser(otherUserId);
        var message = CreateMessage(_testUserId, _now.AddMinutes(-5));

        var result = _resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToEditMessage()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var message = CreateMessage(_testUserId, _now);

        var result = _resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowLikeOthersMessage()
    {
        var user = CreateUser(_testUserId);
        var otherId = Guid.NewGuid();
        var message = CreateMessage(otherId, _now);

        var result = _resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyLikeOwnMessage()
    {
        var user = CreateUser(_testUserId);
        var message = CreateMessage(_testUserId, _now);

        var result = _resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToLikeMessage()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var message = CreateMessage(_testUserId, _now);

        var result = _resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeFalse();
    }
}
