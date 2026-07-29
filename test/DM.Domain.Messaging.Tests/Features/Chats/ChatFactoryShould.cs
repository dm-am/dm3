using DM.Domain.Core.Exceptions;
using System.Net;
using System;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class ChatFactoryShould : UnitTestBase
{
    private readonly ChatFactory _factory;
    private readonly Mock<IGuidFactory> _guidFactory;

    public ChatFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();

        _factory = new ChatFactory(_guidFactory.Object);
    }

    [Fact]
    public void CreateDirectChatWithTwoParticipants()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(chatId);

        var (chat, links) = _factory.CreateDirect(userId, otherUserId);

        chat.Should().NotBeNull();
        chat.ChatId.Should().Be(chatId);
        chat.Type.Should().Be(ChatType.Direct);
        links.Should().HaveCount(2);
        links.Select(l => l.UserId).Should().Contain(new[] { userId, otherUserId });
    }

    [Fact]
    public void ThrowWhenCreatingDirectChatWithSameUser()
    {
        var userId = Guid.NewGuid();
        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var act = () => _factory.CreateDirect(userId, userId);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage("Cannot create a direct chat with yourself*");
    }

    [Fact]
    public void CreateGroupChatWithMultipleParticipants()
    {
        var chatId = Guid.NewGuid();
        var title = "Test Group";
        var participantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        _guidFactory.Setup(f => f.Create()).Returns(chatId);

        var (chat, links) = _factory.CreateGroup(title, participantIds);

        chat.Should().NotBeNull();
        chat.ChatId.Should().Be(chatId);
        chat.Type.Should().Be(ChatType.Group);
        chat.Title.Should().Be(title);
        links.Should().HaveCount(3);
        links.Select(l => l.UserId).Should().BeEquivalentTo(participantIds);
    }

    [Fact]
    public void CreateChatLinksWithNonRemovedStatus()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var (_, links) = _factory.CreateDirect(userId, otherUserId);

        links.All(l => l.IsRemoved == false).Should().BeTrue();
    }

    [Fact]
    public void AssignChatIdToAllLinks()
    {
        var chatId = Guid.NewGuid();
        var participantIds = new[] { Guid.NewGuid(), Guid.NewGuid() };

        _guidFactory.Setup(f => f.Create()).Returns(chatId);

        var (_, links) = _factory.CreateGroup("Group", participantIds);

        links.All(l => l.ChatId == chatId).Should().BeTrue();
    }

    [Fact]
    public void RemoveDuplicateParticipantsInGroupChat()
    {
        var duplicateId = Guid.NewGuid();
        var participantIds = new[] { duplicateId, Guid.NewGuid(), duplicateId };

        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid());

        var (_, links) = _factory.CreateGroup("Group", participantIds);

        links.Should().HaveCount(2);
        links.Select(l => l.UserId).Should().OnlyHaveUniqueItems();
    }
}
