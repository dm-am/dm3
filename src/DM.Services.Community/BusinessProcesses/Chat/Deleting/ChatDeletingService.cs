using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.Core.Exceptions;

namespace DM.Services.Community.BusinessProcesses.Chat.Deleting;

/// <inheritdoc />
internal class ChatDeletingService : IChatDeletingService
{
    private readonly IChatReadingRepository _readingRepository;
    private readonly IChatDeletingRepository _deletingRepository;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public ChatDeletingService(
        IChatReadingRepository readingRepository,
        IChatDeletingRepository deletingRepository,
        IIntentionManager intentionManager)
    {
        _readingRepository = readingRepository;
        _deletingRepository = deletingRepository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public async Task Delete(Guid id)
    {
        var message = await _readingRepository.Get(id);
        if (message == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.Gone, "Message not found");
        }

        _intentionManager.ThrowIfForbidden(ChatIntention.DeleteMessage, message);

        await _deletingRepository.Delete(id);
    }
}
