using System;
using DM.Domain.Core.Abstractions;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Messages;

public class MessageFactoryShould : UnitTestBase
{
    private readonly MessageFactory _factory;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public MessageFactoryShould()
    {
        _guidFactory = Mock<IGuidFactory>();
        _dateTimeProvider = Mock<IDateTimeProvider>();

        _factory = new MessageFactory(
            _guidFactory,
            _dateTimeProvider);
    }

    [Fact]
    public void CreateMessageWithValidProperties()
    {
        var messageId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var createMessage = new CreateMessage
        {
            ChatId = chatId,
            Text = "Test message"
        };

        _guidFactory.Create().Returns(messageId);
        _dateTimeProvider.Now.Returns(now);

        var result = _factory.Create(createMessage, userId);

        result.Should().NotBeNull();
        result.MessageId.Should().Be(messageId);
        result.ChatId.Should().Be(chatId);
        result.UserId.Should().Be(userId);
        result.Text.Should().Be(createMessage.Text);
        result.CreatedUtc.Should().Be(now);
        result.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public void PreserveMessageText()
    {
        var text = "This is a test message with special characters: !@#$%";
        var createMessage = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = text
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createMessage, Guid.NewGuid());

        result.Text.Should().Be(text);
    }

    [Fact]
    public void AssignUserIdCorrectly()
    {
        var userId = Guid.NewGuid();
        var createMessage = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = "Test"
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createMessage, userId);

        result.UserId.Should().Be(userId);
    }

    [Fact]
    public void CreateNonRemovedMessage()
    {
        var createMessage = new CreateMessage
        {
            ChatId = Guid.NewGuid(),
            Text = "Test"
        };

        _guidFactory.Create().Returns(Guid.NewGuid());
        _dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var result = _factory.Create(createMessage, Guid.NewGuid());

        result.IsRemoved.Should().BeFalse();
    }
}
