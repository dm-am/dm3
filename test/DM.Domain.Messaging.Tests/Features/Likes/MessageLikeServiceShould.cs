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
using NSubstitute;
using Xunit;

namespace DM.Domain.Messaging.Tests.Features.Likes;

public class MessageLikeServiceShould : UnitTestBase
{
    private readonly IMessageService _messageService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;
    private readonly MessageLikeService _service;

    public MessageLikeServiceShould()
    {
        _messageService = Mock<IMessageService>();
        _intentionManager = Mock<IIntentionManager>();

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.LikeAsync(Arg.Any<Message>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        _likeOperations.UnlikeAsync(Arg.Any<Message>()).Returns(Task.CompletedTask);

        _service = new MessageLikeService(
            _messageService,
            _intentionManager,
            _likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeAction()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.GetAsync(messageId, default).Returns(message);

        await _service.LikeMessageAsync(messageId);

        _intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Like, message);
    }

    [Fact]
    public async Task CallLikeOperationsWithCorrectEventType()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.GetAsync(messageId, default).Returns(message);

        await _service.LikeMessageAsync(messageId);

        await _likeOperations.Received(1).LikeAsync(message, EventType.LikedMessage);
    }

    [Fact]
    public async Task AuthorizeUnlikeAction()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.GetAsync(messageId, default).Returns(message);

        await _service.UnlikeMessageAsync(messageId);

        _intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Like, message);
    }

    [Fact]
    public async Task CallUnlikeOperations()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        _messageService.GetAsync(messageId, default).Returns(message);

        await _service.UnlikeMessageAsync(messageId);

        await _likeOperations.Received(1).UnlikeAsync(message);
    }
}
