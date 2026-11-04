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
    private readonly MessageFactory factory;
    private readonly IGuidFactory guidFactory;
    private readonly IDateTimeProvider dateTimeProvider;

    public MessageFactoryShould()
    {
        guidFactory = Mock<IGuidFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();

        factory = new MessageFactory(
            guidFactory,
            dateTimeProvider);
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

        guidFactory.Create().Returns(messageId);
        dateTimeProvider.Now.Returns(now);

        var result = factory.Create(createMessage, userId);

        result.MessageId.Should().Be(messageId);
        result.ChatId.Should().Be(chatId);
        result.UserId.Should().Be(userId);
        result.Text.Should().Be(createMessage.Text);
        result.CreatedUtc.Should().Be(now);
        result.IsRemoved.Should().BeFalse();
    }
}
