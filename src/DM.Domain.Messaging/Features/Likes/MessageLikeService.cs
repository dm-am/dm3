using System;
using System.Threading.Tasks;
using DM.Domain.Messaging.Authorization;
using DM.Domain.Messaging.Features.Messages;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Likes;

namespace DM.Domain.Messaging.Features.Likes;

/// <summary>
/// Message like service
/// </summary>
internal class MessageLikeService : IMessageLikeService
{
    private readonly IMessageService _messageService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;

    /// <inheritdoc />
    public MessageLikeService(
        IMessageService messageService,
        IIntentionManager intentionManager,
        ILikeOperations likeOperations)
    {
        _messageService = messageService;
        _intentionManager = intentionManager;
        _likeOperations = likeOperations;
    }

    /// <inheritdoc />
    public async Task<GeneralUser> LikeMessage(Guid messageId)
    {
        var message = await _messageService.GetAsync(messageId);
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        return await _likeOperations.Like(message, EventType.LikedMessage);
    }

    /// <inheritdoc />
    public async Task UnlikeMessage(Guid messageId)
    {
        var message = await _messageService.GetAsync(messageId);
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        await _likeOperations.Unlike(message);
    }
}
