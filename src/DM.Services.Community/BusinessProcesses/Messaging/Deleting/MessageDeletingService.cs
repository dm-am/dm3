using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Messaging.Reading;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Messaging.Deleting;

/// <inheritdoc />
internal class MessageDeletingService : IMessageDeletingService
{
    private readonly IIdentityProvider _identityProvider;
    private readonly IMessageReadingRepository _readingRepository;
    private readonly IMessageDeletingRepository _deletingRepository;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public MessageDeletingService(
        IIdentityProvider identityProvider,
        IMessageReadingRepository readingRepository,
        IMessageDeletingRepository deletingRepository,
        IIntentionManager intentionManager)
    {
        _identityProvider = identityProvider;
        _readingRepository = readingRepository;
        _deletingRepository = deletingRepository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId)
    {
        var currentUserId = _identityProvider.Current.User.UserId;
        var message = await _readingRepository.Get(messageId, currentUserId);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Message not found");
        }

        _intentionManager.ThrowIfForbidden(MessageIntention.Delete, message);

        await _deletingRepository.Delete(messageId, currentUserId);
    }
}
