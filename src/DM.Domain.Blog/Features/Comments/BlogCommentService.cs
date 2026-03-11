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

namespace DM.Domain.Blog.Features.Comments;

/// <inheritdoc />
internal class BlogCommentService : IBlogCommentService
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IBlogService _blogService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IBlogCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _invokedEventProducer;

    public BlogCommentService(
        IValidator<CreateComment> createValidator,
        IValidator<UpdateComment> updateValidator,
        IBlogService blogService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IBlogCommentRepository repository,
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
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var blog = await _blogService.GetBlog(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateComment, blog);

        // Check blacklist
        var currentUserId = _identityProvider.Current.User.UserId;
        if (blog.BlacklistedUserIds.Contains(currentUserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, "You are blacklisted from this blog");
        }

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUserId,
            blog.Id,
            blog.CommentCount + 1);

        await _countersRepository.IncrementAsync(blog.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewBlogComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid blogId, PagingQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _blogService.GetBlog(blogId);

        var totalCount = await _repository.Count(blogId, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(blogId, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.NotFound, $"Comment {commentId} not found");
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await GetAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        var text = updateComment.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text == comment.Text)
        {
            // No actual changes
            return comment;
        }

        var entity = new UpdateBlogCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now
        };

        var updatedComment = await _repository.Update(entity);
        await _invokedEventProducer.SendAsync(EventType.ChangedBlogComment, updateComment.CommentId);
        return updatedComment;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid commentId)
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
            newLastCommentId = await _repository.GetSecondLastCommentId(comment.BlogId);
        }

        var entity = new DeleteBlogCommentEntity
        {
            CommentId = commentId,
            BlogId = comment.BlogId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now,
            NewCommentCount = Math.Max(0, comment.BlogCommentCount - 1),
            NewLastCommentId = newLastCommentId
        };

        await _repository.Delete(entity);
        await _countersRepository.DecrementAsync(comment.BlogId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedBlogComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid blogId)
    {
        await _blogService.GetBlog(blogId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, blogId);
    }
}
