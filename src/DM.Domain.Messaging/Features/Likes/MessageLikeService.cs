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
    public async Task<GeneralUser> LikeMessageAsync(Guid messageId) =>
        await LikeAsync(await _messageService.GetAsync(messageId));

    /// <inheritdoc />
    public async Task UnlikeMessageAsync(Guid messageId) =>
        await UnlikeAsync(await _messageService.GetAsync(messageId));

    /// <inheritdoc />
    public async Task<GeneralUser> LikeGlobalChatMessageAsync(Guid messageId) =>
        await LikeAsync(await _messageService.GetGlobalChatMessageAsync(messageId));

    /// <inheritdoc />
    public async Task UnlikeGlobalChatMessageAsync(Guid messageId) =>
        await UnlikeAsync(await _messageService.GetGlobalChatMessageAsync(messageId));

    private async Task<GeneralUser> LikeAsync(Message message)
    {
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        return await _likeOperations.LikeAsync(message, EventType.LikedMessage);
    }

    private async Task UnlikeAsync(Message message)
    {
        _intentionManager.ThrowIfForbidden(MessageIntention.Like, message);
        await _likeOperations.UnlikeAsync(message);
    }
}
