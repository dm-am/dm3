using System;
using System.Threading.Tasks;
using DM.Services.Common.Authorization;
using DM.Services.Core.Exceptions;
using DM.Services.Common.BusinessProcesses.UnreadCounters;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using PublicationDal = DM.Services.DataAccess.BusinessObjects.Blogs.Publication;
using DM.Services.DataAccess.RelationalStorage;
using DM.Services.MessageQueuing.GeneralBus;
using Comment = DM.Services.DataAccess.BusinessObjects.Common.Comment;

namespace DM.Services.Community.BusinessProcesses.Blogs.Comments.Deleting;

/// <inheritdoc />
internal class CommentDeletingService : ICommentDeletingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IUpdateBuilderFactory _updateBuilderFactory;
    private readonly ICommentDeletingRepository _repository;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IInvokedEventProducer _invokedEventProducer;

    /// <inheritdoc />
    public CommentDeletingService(
        IIntentionManager intentionManager,
        IUpdateBuilderFactory updateBuilderFactory,
        ICommentDeletingRepository repository,
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
        var comment = await _repository.GetForDelete(commentId);
        if (comment == null)
        {
            throw new HttpException(System.Net.HttpStatusCode.NotFound, $"Comment {commentId} not found");
        }

        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, (Services.Common.Dto.Comment) comment);

        var updatePublication = _updateBuilderFactory.Create<PublicationDal>(comment.PublicationId)
            .Field(p => p.CommentCount, Math.Max(0, comment.EntityCommentCount - 1));

        var updateComment = _updateBuilderFactory.Create<Comment>(commentId)
            .Field(c => c.IsRemoved, true);
        await _repository.Delete(updateComment, updatePublication);
        await _unreadCountersRepository.Decrement(comment.PublicationId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.Send(EventType.DeletedBlogComment, commentId);
    }
}
