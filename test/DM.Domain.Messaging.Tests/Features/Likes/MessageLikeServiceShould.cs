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
    private readonly IMessageService messageService;
    private readonly IIntentionManager intentionManager;
    private readonly ILikeOperations likeOperations;
    private readonly MessageLikeService service;

    public MessageLikeServiceShould()
    {
        messageService = Mock<IMessageService>();
        intentionManager = Mock<IIntentionManager>();

        likeOperations = Mock<ILikeOperations>();
        likeOperations.LikeAsync(Arg.Any<Message>(), Arg.Any<EventType>()).Returns(new GeneralUser());
        likeOperations.UnlikeAsync(Arg.Any<Message>()).Returns(Task.CompletedTask);

        service = new MessageLikeService(
            messageService,
            intentionManager,
            likeOperations);
    }

    [Fact]
    public async Task LikeAMessageTheCallerMayLike()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        messageService.GetAsync(messageId, default).Returns(message);

        await service.LikeMessageAsync(messageId);

        intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Like, message);
        await likeOperations.Received(1).LikeAsync(message, EventType.LikedMessage);
    }

    [Fact]
    public async Task UnlikeAMessageTheCallerMayLike()
    {
        var messageId = Guid.NewGuid();
        var message = new Message { Id = messageId };
        messageService.GetAsync(messageId, default).Returns(message);

        await service.UnlikeMessageAsync(messageId);

        intentionManager.Received(1).ThrowIfForbidden(MessageIntention.Like, message);
        await likeOperations.Received(1).UnlikeAsync(message);
    }
}
