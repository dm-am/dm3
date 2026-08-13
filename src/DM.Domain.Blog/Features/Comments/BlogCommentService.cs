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
using DM.Domain.Core.Content;
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

        var blog = await _blogService.GetBlogAsync(createComment.EntityId);
        // The blacklist closes writing: reading this blog is open to the user it
        // blacklisted, commenting in it is not. The rule is part of the intention
        // and is asked here, once.
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateComment, blog);

        var currentUser = _identityProvider.Current.User;

        // Strip [mod] authored by a non-moderator (it renders as a green mod
        // block on the Comment surface); Moderator+ may author it.
        createComment.Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role);

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUser.UserId,
            blog.Id,
            blog.CommentCount + 1);

        await _countersRepository.IncrementAsync(blog.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewBlogComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid blogId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _blogService.GetBlogAsync(blogId);

        var totalCount = await _repository.Count(blogId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(blogId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        return await _repository.Get(commentId) ??
               throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
    }

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await GetAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting a comment publishes text exactly as writing one does, so the
        // ban is asked here too, on the terms BlogIntention.CreateComment sets:
        // the user's own blog stays open. Only the author is asked, a moderator
        // editing somebody else's comment is moderating and a ban takes no
        // moderator tool away. The blog is read inside the condition because
        // whose blog it is only matters to somebody the ban actually restricts.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId && !currentUser.MaySpeak())
        {
            var blog = await _blogService.GetBlogAsync(comment.EntityId);
            currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));
        }

        var text = updateComment.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // Strip [mod] authored by a non-moderator before comparing/saving.
            text = ModBlockSanitizer.SanitizeForAuthor(text, currentUser.Role);
        }
        if (string.IsNullOrEmpty(text) || text == comment.Text)
        {
            // No actual changes
            return comment;
        }

        var entity = new UpdateBlogCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now,
            EditorUserId = currentUser.UserId
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
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
        }

        _intentionManager.ThrowIfForbidden(CommentIntention.Delete, (Comment)comment);

        Guid? newLastCommentId = null;
        if (comment.IsLastComment)
        {
            newLastCommentId = await _repository.GetNewestCommentIdExcept(comment.BlogId, commentId);
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
        await _blogService.GetBlogAsync(blogId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, blogId);
    }
}
