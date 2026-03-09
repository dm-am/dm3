using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Events;
using FluentValidation;

namespace DM.Domain.Blog.Features.PublicationComments;

/// <inheritdoc />
internal class PublicationCommentService : IPublicationCommentService
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IBlogService _blogService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPublicationCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _invokedEventProducer;

    public PublicationCommentService(
        IValidator<CreateComment> createValidator,
        IValidator<UpdateComment> updateValidator,
        IBlogService blogService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IPublicationCommentRepository repository,
        IUnreadCountersRepository countersRepository,
        IEventProducer invokedEventProducer)
    {
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _blogService = blogService;
        _intentionManager = intentionManager;
        _identityProvider = identityProvider;
        _dateTimeProvider = dateTimeProvider;
        _repository = repository;
        _countersRepository = countersRepository;
        _invokedEventProducer = invokedEventProducer;
    }

    /// <inheritdoc />
    public async Task<Comment> Create(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var publication = await _blogService.GetPublication(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.CreateComment, publication);

        // Check blacklist
        var blog = await _blogService.GetBlog(publication.BlogId);
        var currentUserId = _identityProvider.Current.User.UserId;
        if (blog.BlacklistedUserIds.Contains(currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are blacklisted from this blog");
        }

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUserId,
            publication.Id,
            publication.CommentCount + 1);

        await _countersRepository.Increment(publication.Id, UnreadEntryType.Message);
        await _invokedEventProducer.Send(EventType.NewBlogComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> Get(Guid publicationId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _blogService.GetPublication(publicationId);

        var totalCount = await _repository.Count(publicationId, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(publicationId, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> Get(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.NotFound, $"Comment {commentId} not found");
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

        var entity = new UpdatePublicationCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now
        };

        var updatedComment = await _repository.Update(entity);
        await _invokedEventProducer.Send(EventType.ChangedBlogComment, updateComment.CommentId);
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
            newLastCommentId = await _repository.GetSecondLastCommentId(comment.PublicationId);
        }

        var entity = new DeletePublicationCommentEntity
        {
            CommentId = commentId,
            PublicationId = comment.PublicationId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedAtUtc = _dateTimeProvider.Now,
            NewCommentCount = Math.Max(0, comment.PublicationCommentCount - 1),
            NewLastCommentId = newLastCommentId
        };

        await _repository.Delete(entity);
        await _countersRepository.Decrement(comment.PublicationId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.Send(EventType.DeletedBlogComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsRead(Guid publicationId)
    {
        await _blogService.GetPublication(publicationId);
        await _countersRepository.Flush(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, publicationId);
    }
}
