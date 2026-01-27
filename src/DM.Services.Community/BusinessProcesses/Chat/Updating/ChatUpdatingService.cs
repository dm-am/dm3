using System;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Chat.Updating;

/// <inheritdoc />
internal class ChatUpdatingService : IChatUpdatingService
{
    private readonly IChatReadingRepository _readingRepository;
    private readonly IChatUpdatingRepository _updatingRepository;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;

    /// <inheritdoc />
    public ChatUpdatingService(
        IChatReadingRepository readingRepository,
        IChatUpdatingRepository updatingRepository,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider)
    {
        _readingRepository = readingRepository;
        _updatingRepository = updatingRepository;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Update(Guid id, string text)
    {
        var message = await _readingRepository.Get(id);
        if (message == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Message not found");
        }

        _intentionManager.ThrowIfForbidden(ChatIntention.EditMessage, message);

        var editorUserId = _identityProvider.Current.User.UserId;
        return await _updatingRepository.Update(id, text, editorUserId);
    }
}
