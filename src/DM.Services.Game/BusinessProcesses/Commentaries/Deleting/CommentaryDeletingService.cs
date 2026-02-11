using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Game.Authorization;
using DM.Services.Game.BusinessProcesses.Commentaries.Reading;
using DM.Services.Game.BusinessProcesses.Commentaries.Updating;
using DM.Services.MessageQueuing.GeneralBus;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Game.BusinessProcesses.Commentaries.Deleting;

/// <inheritdoc />
internal class CommentaryDeletingService : ICommentaryDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentaryReadingService _readingService;
    private readonly ICommentaryUpdatingRepository _updatingRepository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentaryDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentaryReadingService readingService,
        ICommentaryUpdatingRepository updatingRepository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _readingService = readingService;
        _updatingRepository = updatingRepository;
        _unreadCountersRepository = unreadCountersRepository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task Delete(Guid commentId)
    {
        var comment = await _readingService.Get(commentId);
        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, comment);

        var updateComment = _updateBuilderFactory.Create<Comment>(commentId)
            .Field(c => c.IsRemoved, true);
        await _updatingRepository.Update(updateComment);
        await _unreadCountersRepository.Decrement(comment.EntityId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.Send(EventType.DeletedGameComment, commentId);
    }
}