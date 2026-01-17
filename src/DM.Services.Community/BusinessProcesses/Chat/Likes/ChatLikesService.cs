using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.Likes;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;

namespace DM.Services.Community.BusinessProcesses.Chat.Likes;

/// <inheritdoc cref="IChatLikesService" />
internal class ChatLikesService : LikeServiceBase, IChatLikesService
{
    private readonly IChatReadingRepository _readingRepository;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public ChatLikesService(
        IChatReadingRepository readingRepository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        ILikeFactory likeFactory,
        ILikeRepository likeRepository,
        IInvokedEventProducer invokedEventProducer)
        : base(identityProvider, likeFactory, likeRepository, invokedEventProducer)
    {
        _readingRepository = readingRepository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Like(Guid id)
    {
        var message = await _readingRepository.Get(id);
        _intentionManager.ThrowIfForbidden(ChatIntention.LikeMessage, message);
        await base.Like(message, EventType.LikedChatMessage);
        return await _readingRepository.Get(id);
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Unlike(Guid id)
    {
        var message = await _readingRepository.Get(id);
        _intentionManager.ThrowIfForbidden(ChatIntention.LikeMessage, message);
        await Dislike(message);
        return await _readingRepository.Get(id);
    }
}
