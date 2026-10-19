using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
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
    private readonly IEventProducer _eventProducer;

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
        IEventProducer eventProducer)
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
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var topic = await _topicService.GetAsync(createComment.EntityId);
        // MayCreate's question, so the form the page offers and this refusal
        // cannot part.
        _intentionManager.ThrowIfForbidden(TopicIntention.CreateComment, topic);

        // A topic is a public place: the author's personal blacklist closes it to
        // nobody, keeping somebody out of the forum is a ban.
        var currentUser = _identityProvider.Current.User;

        var createEntity = new CreateTopicCommentEntity
        {
            TopicId = topic.Id,
            AuthorId = currentUser.UserId,
            // [mod] renders as a mod block on the Comment surface; Moderator+ may author it.
            Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role)
        };

        var createdComment = await _repository.Create(createEntity);
        await _countersRepository.IncrementAsync(topic.Id, UnreadEntryType.Message);
        await _eventProducer.SendAsync(EventType.NewTopicComment, createdComment.Id);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The one gate CreateAsync is refused by: a closed topic and the ban are
    /// both in it (TopicIntentionResolver.CreateComment).
    /// </remarks>
    public bool MayCreate(Topics.Topic topic) =>
        _intentionManager.IsAllowed(TopicIntention.CreateComment, topic);

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid topicId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _topicService.GetAsync(topicId);

        var totalCount = await _repository.Count(topicId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);

        var comments = await _repository.Get(topicId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // The discussions share one table: a comment of a topic not there to the
        // reader answers as one that is not there, or the id would tell a stranger what it resolves to.
        await DiscussionRefusal.AsCommentNotFound(commentId, () => _topicService.GetAsync(comment.EntityId));

        return comment;
    }

    /// <summary>
    /// The comment by its identifier, among the comments of topics only, so this
    /// route cannot edit a comment of a game or a blog past that discussion's blacklist.
    /// </summary>
    private async Task<Comment> FindAsync(Guid commentId) =>
        await _repository.Get(commentId) ??
        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));

    /// <inheritdoc />
    public async Task<FirstUnreadComment> GetFirstUnreadAsync(string boardAlias, int topicNumber,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        // Board access is the topic lookup itself: a topic this reader may not
        // see never comes back from it, exactly as on the reading paths above.
        var topic = await _topicService.GetByBoardAndNumberAsync(boardAlias, topicNumber);

        // A guest has no marker of his own; no marker at all means everything is
        // unread, and the reader starts at the top.
        var user = _identityProvider.Current.User;
        var lastRead = user.IsAuthenticated
            ? await _countersRepository.GetLastReadTimeAsync(user.UserId, topic.Id, UnreadEntryType.Message)
            : null;
        var lastReadUtc = lastRead.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(lastRead.Value, DateTimeKind.Utc))
            : DateTimeOffset.MinValue;

        // Nothing unread is the ordinary state after opening the topic flushes the
        // marker, and the reader wants the end of the discussion then.
        return await _repository.FindFirstUnread(topic.Id, lastReadUtc, excludeUserIds)
               ?? await _repository.GetLastComment(topic.Id, excludeUserIds)
               ?? new FirstUnreadComment();
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await FindAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting publishes text as writing does, so the author is asked the ban
        // too; a moderator editing somebody else's comment is moderating.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            currentUser.ThrowIfMayNotComment();
        }

        var text = AuthoredText.AsTyped(updateComment.Text);
        if (!string.IsNullOrEmpty(text))
        {
            text = ModBlockSanitizer.SanitizeForAuthor(text, currentUser.Role);
        }
        if (string.IsNullOrEmpty(text) || text == comment.Text)
        {
            return comment;
        }

        var updateEntity = new UpdateTopicCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now,
            EditorUserId = currentUser.UserId
        };

        var updatedComment = await _repository.Update(updateEntity);
        await _eventProducer.SendAsync(EventType.ChangedTopicComment, updateComment.CommentId);
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

        var deleteComment = new DeleteTopicCommentEntity
        {
            CommentId = commentId,
            TopicId = comment.TopicId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        // The topic's last-comment pointer moves inside Delete, in the transaction
        // that marks the comment.
        var removed = await _repository.Delete(deleteComment);
        if (!removed)
        {
            // Somebody else removed it in between; the counter and the event are that removal's.
            return;
        }

        await _countersRepository.DecrementAsync(comment.EntityId, UnreadEntryType.Message, comment.CreatedUtc);

        await _eventProducer.SendAsync(EventType.DeletedTopicComment, commentId);
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
        // Identifiers only: the counters of the full list would be zeroed a line later.
        var boards = await _boardService.GetBoardsWithoutCounters();
        var userId = _identityProvider.Current.User.UserId;
        foreach (var board in boards)
        {
            await _countersRepository.FlushAllAsync(userId, UnreadEntryType.Message, board.Id);
        }
    }
}
