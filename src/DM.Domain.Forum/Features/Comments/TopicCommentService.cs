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
using DM.Domain.Core.Content;
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
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var topic = await _topicService.GetAsync(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(TopicIntention.CreateComment, topic);

        // Check if topic author has blocked the current user
        var currentUser = _identityProvider.Current.User;
        if (topic.Author != null && await _userBlacklistChecker.IsBlockedAsync(topic.Author.UserId, currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "Вы не можете комментировать этот топик");
        }

        var createEntity = new CreateTopicCommentEntity
        {
            TopicId = topic.Id,
            AuthorId = currentUser.UserId,
            // Strip [mod] authored by a non-moderator (it renders as a green
            // mod block on the Comment surface); Moderator+ may author it.
            Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role)
        };

        var createdComment = await _repository.Create(createEntity);
        await _countersRepository.IncrementAsync(topic.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewTopicComment, createdComment.Id);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid topicId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _topicService.GetAsync(topicId);

        var totalCount = await _repository.Count(topicId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(topicId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
    }

    /// <inheritdoc />
    public async Task<FirstUnreadComment> GetFirstUnreadAsync(string boardAlias, int topicNumber,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        // Board access is the topic lookup itself: a topic this reader may not
        // see never comes back from it, exactly as on the reading paths above.
        var topic = await _topicService.GetByBoardAndNumberAsync(boardAlias, topicNumber);

        // A guest has no marker of his own and the anonymous one belongs to
        // nobody, so asking for it would answer with somebody else's reading.
        // No marker at all means the same thing for an authenticated reader who
        // has never opened the topic: everything is unread, he starts at the top.
        var user = _identityProvider.Current.User;
        var lastRead = user.IsAuthenticated
            ? await _countersRepository.GetLastReadTimeAsync(user.UserId, topic.Id, UnreadEntryType.Message)
            : null;
        var lastReadUtc = lastRead.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(lastRead.Value, DateTimeKind.Utc))
            : DateTimeOffset.MinValue;

        // Nothing unread is the ordinary state one second after the topic was
        // opened, because opening it flushes the marker. The end of the
        // discussion is what the reader wants there, never its beginning: the
        // beginning is what every repeat visit used to get.
        return await _repository.FindFirstUnread(topic.Id, lastReadUtc, excludeUserIds)
               ?? await _repository.GetLastComment(topic.Id, excludeUserIds)
               ?? new FirstUnreadComment();
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await GetAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting a comment publishes text exactly as writing one does, so the
        // ban is asked here too. Only the author is asked: a moderator editing
        // somebody else's comment is moderating, and a ban takes no moderator
        // tool away. The forum has no own-space exemption, the same answer
        // TopicIntention.CreateComment gives.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            currentUser.ThrowIfMayNotComment();
        }

        var text = updateComment.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // Strip [mod] authored by a non-moderator before comparing/saving.
            text = ModBlockSanitizer.SanitizeForAuthor(text, _identityProvider.Current.User.Role);
        }
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
        await _invokedEventProducer.SendAsync(EventType.ChangedTopicComment, updateComment.CommentId);
        return updatedComment;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid commentId)
    {
        var comment = await _repository.GetForDelete(commentId);
        if (comment == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
        }

        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, (Comment)comment);

        Guid? newLastCommentId = null;
        if (comment.IsLastComment)
        {
            newLastCommentId = await _repository.GetNewestCommentIdExcept(comment.TopicId, commentId);
        }

        var deleteComment = new DeleteTopicCommentEntity
        {
            CommentId = commentId,
            TopicId = comment.TopicId,
            NewLastCommentId = newLastCommentId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        await _repository.Delete(deleteComment);
        await _countersRepository.DecrementAsync(comment.EntityId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedTopicComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid topicId)
    {
        await _topicService.GetAsync(topicId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, topicId);
    }

    /// <inheritdoc />
    public async Task MarkBoardAsReadAsync(string boardTitle)
    {
        var board = await _boardService.GetBoard(boardTitle);
        await _countersRepository.FlushAllAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, board.Id);
    }

    /// <inheritdoc />
    public async Task MarkAllAsReadAsync()
    {
        var boards = await _boardService.GetBoardsList();
        var userId = _identityProvider.Current.User.UserId;
        foreach (var board in boards)
        {
            await _countersRepository.FlushAllAsync(userId, UnreadEntryType.Message, board.Id);
        }
    }
}
