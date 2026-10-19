using System;
using DM.Domain.Core.Identity;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Configuration;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Abstractions;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Authorization;

public class MessageIntentionResolverShould : UnitTestBase
{
    private const int EditTimeoutMinutes = 60;

    private readonly MessageIntentionResolver resolver;
    private readonly Guid testUserId = Guid.NewGuid();
    private readonly DateTime now = DateTime.UtcNow;

    public MessageIntentionResolverShould()
    {
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(now);

        var options = Options.Create(new MessagingConfiguration { EditTimeoutMinutes = EditTimeoutMinutes });

        resolver = new MessageIntentionResolver(options, dateTimeProvider);
    }

    private static AuthenticatedUser CreateUser(Guid userId, UserRole role = UserRole.RegularUser) =>
        Create.User(userId).WithRole(role).Please();

    private static Message CreateMessage(Guid authorId, DateTime createdUtc, ChatType chatType = ChatType.Direct)
    {
        return new Message
        {
            Author = new GeneralUser { UserId = authorId },
            CreatedUtc = createdUtc,
            ChatType = chatType
        };
    }

    /// <summary>
    /// The window closes after its last minute: the minute itself still belongs to it.
    /// </summary>
    [Theory]
    [InlineData(MessageIntention.Edit, EditTimeoutMinutes / 2, true)]
    [InlineData(MessageIntention.Edit, EditTimeoutMinutes, true)]
    [InlineData(MessageIntention.Edit, EditTimeoutMinutes + 1, false)]
    [InlineData(MessageIntention.Delete, EditTimeoutMinutes / 2, true)]
    [InlineData(MessageIntention.Delete, EditTimeoutMinutes, true)]
    [InlineData(MessageIntention.Delete, EditTimeoutMinutes + 1, false)]
    public void LetTheAuthorEditAndDeleteWithinTheWindow(MessageIntention intention, int minutesAgo, bool allowed)
    {
        var user = CreateUser(testUserId);
        var message = CreateMessage(testUserId, now.AddMinutes(-minutesAgo));

        resolver.IsAllowed(user, intention, message).Should().Be(allowed);
    }

    [Fact]
    public void AllowModeratorToEditAnyMessageOfTheGlobalChat()
    {
        var moderatorId = Guid.NewGuid();
        var user = CreateUser(moderatorId, UserRole.Moderator);
        var message = CreateMessage(testUserId, now.AddDays(-10), ChatType.Global);

        var result = resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void AllowAdminToDeleteAnyMessageOfTheGlobalChat()
    {
        var adminId = Guid.NewGuid();
        var user = CreateUser(adminId, UserRole.Admin);
        var message = CreateMessage(testUserId, now.AddDays(-10), ChatType.Global);

        var result = resolver.IsAllowed(user, MessageIntention.Delete, message);

        result.Should().BeTrue();
    }

    /// <summary>
    /// The global chat is the one chat moderated: a direct message, a group chat and the chat of a game room
    /// are edited and deleted by their authors alone, whatever the rank of the reader.
    /// </summary>
    [Theory]
    [InlineData(ChatType.Direct, UserRole.Moderator, MessageIntention.Edit)]
    [InlineData(ChatType.Group, UserRole.SeniorModerator, MessageIntention.Delete)]
    [InlineData(ChatType.GameRoom, UserRole.Admin, MessageIntention.Delete)]
    [InlineData(ChatType.GameRoom, UserRole.Moderator, MessageIntention.Edit)]
    public void DenyModerationAMessageOutsideTheGlobalChat(ChatType chatType, UserRole role, MessageIntention intention)
    {
        var user = CreateUser(Guid.NewGuid(), role);
        var message = CreateMessage(testUserId, now.AddDays(-10), chatType);

        resolver.IsAllowed(user, intention, message).Should().BeFalse();
    }

    [Fact]
    public void DenyNonAuthorToEditMessage()
    {
        var otherUserId = Guid.NewGuid();
        var user = CreateUser(otherUserId);
        var message = CreateMessage(testUserId, now.AddMinutes(-5));

        var result = resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToEditMessage()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var message = CreateMessage(testUserId, now);

        var result = resolver.IsAllowed(user, MessageIntention.Edit, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void AllowLikeOthersMessage()
    {
        var user = CreateUser(testUserId);
        var otherId = Guid.NewGuid();
        var message = CreateMessage(otherId, now);

        var result = resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeTrue();
    }

    [Fact]
    public void DenyLikeOwnMessage()
    {
        var user = CreateUser(testUserId);
        var message = CreateMessage(testUserId, now);

        var result = resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeFalse();
    }

    [Fact]
    public void DenyGuestToLikeMessage()
    {
        var user = CreateUser(Guid.Empty, UserRole.Guest);
        var message = CreateMessage(testUserId, now);

        var result = resolver.IsAllowed(user, MessageIntention.Like, message);

        result.Should().BeFalse();
    }
}
