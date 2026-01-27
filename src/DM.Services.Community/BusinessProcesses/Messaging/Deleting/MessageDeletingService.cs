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
    private readonly IIdentityProvider identityProvider;
    private readonly IMessageReadingRepository readingRepository;
    private readonly IMessageDeletingRepository deletingRepository;
    private readonly IIntentionManager intentionManager;

    /// <inheritdoc />
    public MessageDeletingService(
        IIdentityProvider identityProvider,
        IMessageReadingRepository readingRepository,
        IMessageDeletingRepository deletingRepository,
        IIntentionManager intentionManager)
    {
        this.identityProvider = identityProvider;
        this.readingRepository = readingRepository;
        this.deletingRepository = deletingRepository;
        this.intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId)
    {
        var currentUserId = identityProvider.Current.User.UserId;
        var message = await readingRepository.Get(messageId, currentUserId);
        if (message == null)
        {
            throw new HttpException(HttpStatusCode.Gone, "Message not found");
        }

        intentionManager.ThrowIfForbidden(MessageIntention.Delete, message);

        await deletingRepository.Delete(messageId, currentUserId);
    }
}
