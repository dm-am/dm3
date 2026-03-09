using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Events;
using DM.Domain.Core.UnreadCounters;
using FluentValidation;

namespace DM.Domain.Forum.Features.Comments;

/// <inheritdoc />
internal class TopicCommentService : ITopicCommentService
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly ITopicService _topicService;
    private readonly IBoardService _boardService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ITopicCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _invokedEventProducer;
    private readonly IUserBlacklistChecker _userBlacklistChecker;

    public TopicCommentService(
        IValidator<CreateComment> createValidator,
        IValidator<UpdateComment> updateValidator,
        ITopicService topicService,
        IBoardService boardService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        ITopicCommentRepository repository,
        IUnreadCountersRepository countersRepository,
        IEventProducer invokedEventProducer,
        IUserBlacklistChecker userBlacklistChecker)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _topicService = topicService;
        _boardService = boardService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _countersRepository = countersRepository;
        _invokedEventProducer = invokedEventProducer;
        _userBlacklistChecker = userBlacklistChecker;
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var topic = await _topicService.GetAsync(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(TopicIntention.CreateComment, topic);

        // Check if topic author has blocked the current user
        var currentUserId = _identityProvider.Current.User.UserId;
        if (topic.Author != null && await _userBlacklistChecker.IsBlocked(topic.Author.UserId, currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You cannot comment on this topic");
        }

        var createEntity = new CreateTopicCommentEntity
        {
            TopicId = topic.Id,
            AuthorId = currentUserId,
            Text = createComment.Text,
            NewCommentCount = topic.TotalCommentsCount + 1
        };

        var createdComment = await _repository.Create(createEntity);
        await _countersRepository.Increment(topic.Id, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.NewTopicComment, createdComment.Id);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> Get(Guid topicId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _topicService.GetAsync(topicId);

        var totalCount = await _repository.Count(topicId, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(topicId, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> Get(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.Gone, $"Comment {commentId} not found");
    }

    /// <inheritdoc />
    public async Task<Comment> Update(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await Get(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        var text = updateComment.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text == comment.Text)
        {
            // No actual changes
            return comment;
        }

        var updateEntity = new UpdateTopicCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now
        };

        var updatedComment = await _repository.Update(updateEntity);
        await _invokedEventProducer.Send(EventType.ChangedTopicComment, updateComment.CommentId);
        return updatedComment;
    }

    /// <inheritdoc />
    public async Task Delete(Guid commentId)
    {
        var comment = await _repository.GetForDelete(commentId);
        if (comment == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, $"Comment {commentId} not found");
        }

        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, (Comment)comment);

        Guid? newLastCommentId = null;
        if (comment.IsLastComment)
        {
            newLastCommentId = await _repository.GetSecondLastCommentId(comment.TopicId);
        }

        var deleteComment = new DeleteTopicCommentEntity
        {
            CommentId = commentId,
            TopicId = comment.TopicId,
            NewCommentCount = Math.Max(0, comment.TopicCommentCount - 1),
            NewLastCommentId = newLastCommentId
        };

        await _repository.Delete(deleteComment);
        await _countersRepository.Decrement(comment.EntityId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.Send(EventType.DeletedTopicComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid topicId)
    {
        await _topicService.GetAsync(topicId);
        await _countersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, topicId);
    }

    /// <inheritdoc />
    public async Task MarkBoardAsRead(string boardTitle)
    {
        var board = await _boardService.GetBoard(boardTitle);
        await _countersRepository.FlushAll(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, board.Id);
    }

    /// <inheritdoc />
    public async Task MarkAsRead()
    {
        var boards = await _boardService.GetBoardsList();
        var userId = _identityProvider.Current.User.UserId;
        foreach (var board in boards)
        {
            await _countersRepository.FlushAll(userId, UnreadEntryType.Message, board.Id);
        }
    }
}
