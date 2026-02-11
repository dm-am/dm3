using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Likes;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Messaging.Likes;

/// <summary>
/// Message like service
/// </summary>
internal class MessageLikeService : LikeServiceBase, IMessageLikeService
{
    private readonly IMessageReadingService _messageReadingService;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public MessageLikeService(
        IMessageReadingService messageReadingService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeFactory likeFactory,
        ILikeRepository likeRepository,
        IInvokedEventProducer invokedEventProducer)
        : base(identityProvider, likeFactory, likeRepository, invokedEventProducer)
    {
        _messageReadingService = messageReadingService;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeMessage(Guid messageId)
    {
        var message = await _messageReadingService.Get(messageId);
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        return await Like(message, EventType.LikedMessage);
    }

    /// <inheritdoc />
    public async Task DislikeMessage(Guid messageId)
    {
        var message = await _messageReadingService.Get(messageId);
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        await Dislike(message);
    }
}
