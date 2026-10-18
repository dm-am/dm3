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
    private readonly IBbTagNameCase _tagNames;

    public BlogCommentService(
        IValidator<CreateComment> createValidator,
        IValidator<UpdateComment> updateValidator,
        IBlogService blogService,
        IIntentionManager intentionManager,
        IIdentityProvider identityProvider,
        IDateTimeProvider dateTimeProvider,
        IBlogCommentRepository repository,
        IUnreadCountersRepository countersRepository,
        IEventProducer invokedEventProducer,
        IBbTagNameCase tagNames)
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
        _tagNames = tagNames;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        // The visible read: a blog the user may not open takes no comment and
        // answers as a blog that is not there.
        var blog = await _blogService.GetAsync(createComment.EntityId);
        var currentUser = _identityProvider.Current.User;

        // The blacklist closes writing: reading this blog is open to the user it
        // blacklisted, commenting in it is not. The intention carries the same
        // rule; asked first here, it answers with the reason instead of the
        // generic refusal of the gate below. The gate is MayCreate's question,
        // so the form the page offers and this refusal cannot part.
        blog.ThrowIfBlacklisted(currentUser.UserId);
        _intentionManager.ThrowIfForbidden(BlogIntention.CreateComment, blog);

        // Strip [mod] authored by a non-moderator (it renders as a green mod
        // block on the Comment surface); Moderator+ may author it.
        createComment.Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role), BbTagSet.WithModBlock);

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUser.UserId,
            blog.Id);

        await _countersRepository.IncrementAsync(blog.Id, UnreadEntryType.Message);
        await _invokedEventProducer.SendAsync(EventType.NewBlogComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The intention CreateAsync is refused by, and nothing beside it: the
    /// blacklist CreateAsync asks first is one of the intention's own arms,
    /// asked there for the words only. A private draft and the ban are both in
    /// it (BlogIntentionResolver.CreateComment).
    /// </remarks>
    public bool MayCreate(Blogs.Blog blog) =>
        _intentionManager.IsAllowed(BlogIntention.CreateComment, blog);

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid blogId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        // The discussion of a blog the reader may not open is not there, exactly
        // as the blog is not (see BlogService.GetAsync). It used to be read
        // through GetBlogAsync, which skips the private-draft and premoderation
        // gates, so the comments of a hidden blog were listed to anybody holding
        // its id.
        if (!await _blogService.IsVisibleToViewerAsync(blogId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        var totalCount = await _repository.Count(blogId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(blogId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // The four discussions share one table and the row above is read by its
        // id alone, so the discussion is asked here exactly as the listing asks it:
        // whether the reader may open the blog at all. A comment of a blog shut to
        // the reader, and a comment of another kind of discussion, answer exactly
        // as a comment that is not there - a different answer would tell a stranger
        // that the id resolves, and to what.
        if (!await _blogService.IsVisibleToViewerAsync(comment.EntityId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));
        }

        return comment;
    }

    /// <summary>
    /// The comment by its identifier, among the comments of blogs only (the
    /// repository narrows it: a lookup by id alone let this route edit a comment
    /// of another discussion past that discussion's rules). The edit keeps to
    /// this lookup: who may edit is CommentIntention.Edit's answer on the row.
    /// </summary>
    private async Task<Comment> FindAsync(Guid commentId) =>
        await _repository.Get(commentId) ??
        throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CommentNotFound(commentId));

    /// <inheritdoc />
    public async Task<Comment> UpdateAsync(UpdateComment updateComment)
    {
        await _updateValidator.ValidateAndThrowAsync(updateComment);
        var comment = await FindAsync(updateComment.CommentId);

        _intentionManager.ThrowIfForbidden(CommentIntention.Edit, comment);

        // Rewriting a comment publishes text exactly as writing one does, so the
        // blacklist and the ban are asked here too, on the terms
        // BlogIntention.CreateComment sets: the user's own blog stays open to the
        // ban. Only the author is asked, a moderator editing somebody else's
        // comment is moderating and neither list takes a moderator tool away.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            var blog = await _blogService.GetBlogAsync(comment.EntityId);
            blog.ThrowIfBlacklisted(currentUser.UserId);
            if (!currentUser.MaySpeak())
            {
                currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));
            }
        }

        var text = updateComment.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            // Strip [mod] authored by a non-moderator before comparing/saving.
            text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(text, currentUser.Role), BbTagSet.WithModBlock);
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

        var entity = new DeleteBlogCommentEntity
        {
            CommentId = commentId,
            BlogId = comment.BlogId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        // The blog's comment count and its last-comment pointer move inside
        // Delete, in the transaction that marks the comment.
        var removed = await _repository.Delete(entity);
        if (!removed)
        {
            // Somebody else removed this comment between the read above and the
            // write. The blog's unread counter and the event belong to the removal
            // that happened, and repeating them here takes the counter down twice
            // for one comment. The caller is answered the same either way: the
            // comment is gone, which is what they asked for.
            return;
        }

        await _countersRepository.DecrementAsync(comment.BlogId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedBlogComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid blogId)
    {
        // A discussion the reader cannot see has nothing to mark, and answers as
        // the blog not being there, the way the listing does.
        if (!await _blogService.IsVisibleToViewerAsync(blogId))
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.BlogNotFound);
        }

        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, blogId);
    }
}
