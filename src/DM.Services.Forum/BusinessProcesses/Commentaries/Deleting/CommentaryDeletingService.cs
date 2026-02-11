using System;
using System.Net;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Exceptions;
using DM.Services.DataAccess.BusinessObjects.Common;
using TopicDal = DM.Services.DataAccess.BusinessObjects.Boards.Topic;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.Forum.Authorization;
using DM.Services.MessageQueuing.GeneralBus;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Forum.BusinessProcesses.Commentaries.Deleting;

/// <inheritdoc />
internal class CommentaryDeletingService : ICommentaryDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentaryDeletingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentaryDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentaryDeletingRepository repository,
        IUnreadCountersRepository unreadCountersRepository,
        IInvokedEventProducer invokedEventProducer)
    {
        _intentionManager = intentionManager;
        _updateBuilderFactory = updateBuilderFactory;
        _repository = repository;
        _unreadCountersRepository = unreadCountersRepository;
        _invokedEventProducer = invokedEventProducer;
    }
        
    /// <inheritdoc />
    public async Task Delete(Guid commentId)
    {
        var comment = await _repository.GetForDelete(commentId) ??
            throw new HttpException(HttpStatusCode.Gone, $"Comment {commentId} not found");
        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, comment);

        var updateTopic = _updateBuilderFactory.Create<TopicDal>(comment.EntityId);
        if (comment.IsLastCommentOfTopic)
        {
            var previousCommentaryId = await _repository.GetSecondLastCommentId(comment.EntityId);
            updateTopic = updateTopic.Field(t => t.LastCommentId, previousCommentaryId);
        }

        var updateComment = _updateBuilderFactory.Create<Comment>(commentId)
            .Field(c => c.IsRemoved, true);
        await _repository.Delete(updateComment, updateTopic);
        await _unreadCountersRepository.Decrement(comment.EntityId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.Send(EventType.DeletedForumComment, commentId);
    }
}