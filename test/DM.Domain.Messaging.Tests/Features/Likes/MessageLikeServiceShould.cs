using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Likes;
using DM.Domain.Messaging.Features.Messages;
using DM.Testing;
using Moq;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Likes;

public class MessageLikeServiceShould : UnitTestBase
{
    private readonly Mock<IMessageService> _messageService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<ILikeOperations> _likeOperations;
    private readonly MessageLikeService _service;

    public MessageLikeServiceShould()
    {
        _messageService = Mock<IMessageService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<MessageIntention>(), It.IsAny<Message>()));

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.Setup(l => l.LikeAsync(It.IsAny<Message>(), It.IsAny<EventType>()))
            .ReturnsAsync(new GeneralUser());
        _likeOperations.Setup(l => l.UnlikeAsync(It.IsAny<Message>()))
            .Returns(Task.CompletedTask);

        _service = new MessageLikeService(
            _messageService.Object,
            _intentionManager.Object,
            _likeOperations.Object);
    }

    [Fact]
    public async Task AuthorizeLikeAction()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.Setup(s => s.GetAsync(messageId, default)).ReturnsAsync(message);

        await _service.LikeMessageAsync(messageId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(MessageIntention.Like, message), Times.Once);
    }

    [Fact]
    public async Task CallLikeOperationsWithCorrectEventType()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.Setup(s => s.GetAsync(messageId, default)).ReturnsAsync(message);

        await _service.LikeMessageAsync(messageId);

        _likeOperations.Verify(l => l.LikeAsync(message, EventType.LikedMessage), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUnlikeAction()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.Setup(s => s.GetAsync(messageId, default)).ReturnsAsync(message);

        await _service.UnlikeMessageAsync(messageId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(MessageIntention.Like, message), Times.Once);
    }

    [Fact]
    public async Task CallUnlikeOperations()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.Setup(s => s.GetAsync(messageId, default)).ReturnsAsync(message);

        await _service.UnlikeMessageAsync(messageId);

        _likeOperations.Verify(l => l.UnlikeAsync(message), Times.Once);
    }
}
