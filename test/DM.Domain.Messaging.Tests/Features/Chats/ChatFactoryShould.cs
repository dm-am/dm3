using DM.Domain.Core.Exceptions;
using System.Net;
using System;
using System.Linq;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Messaging.Features.Chats;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Chats;

public class ChatFactoryShould : UnitTestBase
{
    private readonly ChatFactory factory;
    private readonly IGuidFactory guidFactory;

    public ChatFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();

        factory = new ChatFactory(guidFactory);
    }

    [Fact]
    public void CreateDirectChatWithTwoParticipants()
    {
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        guidFactory.Create().Returns(chatId);

        var (chat, links) = factory.CreateDirect(userId, otherUserId);

        chat.ChatId.Should().Be(chatId);
        chat.Type.Should().Be(ChatType.Direct);
        links.Should().HaveCount(2);
        links.Select(l => l.UserId).Should().Contain(new[] { userId, otherUserId });
        links.Should().OnlyContain(l => !l.IsRemoved);
    }

    [Fact]
    public void ThrowWhenCreatingDirectChatWithSameUser()
    {
        var userId = Guid.NewGuid();
        guidFactory.Create().Returns(Guid.NewGuid());

        var act = () => factory.CreateDirect(userId, userId);

        act.Should().Throw<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.BadRequest)
            .WithMessage("Нельзя начать чат с самим собой*");
    }

    [Fact]
    public void CreateGroupChatWithMultipleParticipants()
    {
        var chatId = Guid.NewGuid();
        var title = "Test Group";
        var participantIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        guidFactory.Create().Returns(chatId);

        var (chat, links) = factory.CreateGroup(title, participantIds);

        chat.ChatId.Should().Be(chatId);
        chat.Type.Should().Be(ChatType.Group);
        chat.Title.Should().Be(title);
        links.Should().HaveCount(3);
        links.Select(l => l.UserId).Should().BeEquivalentTo(participantIds);
        links.Should().OnlyContain(l => l.ChatId == chatId);
    }
}
