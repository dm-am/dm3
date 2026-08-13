using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Blog.Authorization;
using DM.Domain.Blog.Features.Blogs;
using DM.Domain.Blog.Features.Publications;
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

namespace DM.Domain.Blog.Features.PublicationComments;

/// <inheritdoc />
internal class PublicationCommentService : IPublicationCommentService
{
    private readonly IValidator<CreateComment> _createValidator;
    private readonly IValidator<UpdateComment> _updateValidator;
    private readonly IBlogService _blogService;
    private readonly IPublicationService _publicationService;
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
        IPublicationService publicationService,
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
        _publicationService = publicationService;
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

        var publication = await _publicationService.GetPublication(createComment.EntityId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.CreateComment, publication);

        // Check blacklist
        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        var currentUser = _identityProvider.Current.User;
        if (blog.BlacklistedUserIds.Contains(currentUser.UserId))
        {
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.BlacklistedFromBlog);
        }

        // Ban check lives here rather than in PublicationIntentionResolver: the
        // rule is about whose blog this is, and the publication carries no blog
        // ownership. The blog is loaded above anyway. The rule itself is not
        // duplicated: BlogRoleExtensions.IsOwnBlog owns "whose blog this is", and
        // AccessRestrictions owns what a ban does with the answer, refusal
        // included.
        currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));

        // Strip [mod] authored by a non-moderator (it renders as a green mod
        // block on the Comment surface); Moderator+ may author it.
        createComment.Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role);

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUser.UserId,
            publication.Id,
            publication.CommentCount + 1);

        await _countersRepository.IncrementAsync(publication.Id, UnreadEntryType.Message);
        // The publication event, not the blog one: a comment carries the id of
        // what it hangs on, so the blog generator joined Comments to Blogs on a
        // publication id, matched nothing and produced no notification. Nothing
        // failed — the author of a commented publication simply was never told.
        await _invokedEventProducer.SendAsync(EventType.NewPublicationComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid publicationId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _publicationService.GetPublication(publicationId);

        var totalCount = await _repository.Count(publicationId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(publicationId, query, paging, excludeUserIds);

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
        // ban is asked here too, on the same terms as in CreateAsync. Only the
        // author is asked, a moderator editing somebody else's comment is
        // moderating and a ban takes no moderator tool away. Reaching the blog
        // from a comment costs two reads, so they are made only for somebody the
        // ban actually restricts.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId && !currentUser.MaySpeak())
        {
            var publication = await _publicationService.GetPublication(comment.EntityId);
            var blog = await _blogService.GetBlogAsync(publication.BlogId);
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

        var entity = new UpdatePublicationCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now,
            EditorUserId = currentUser.UserId
        };

        var updatedComment = await _repository.Update(entity);
        await _invokedEventProducer.SendAsync(EventType.ChangedPublicationComment, updateComment.CommentId);
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
            newLastCommentId = await _repository.GetNewestCommentIdExcept(comment.PublicationId, commentId);
        }

        var entity = new DeletePublicationCommentEntity
        {
            CommentId = commentId,
            PublicationId = comment.PublicationId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now,
            NewCommentCount = Math.Max(0, comment.PublicationCommentCount - 1),
            NewLastCommentId = newLastCommentId
        };

        await _repository.Delete(entity);
        await _countersRepository.DecrementAsync(comment.PublicationId, UnreadEntryType.Message, comment.CreatedUtc);

        await _invokedEventProducer.SendAsync(EventType.DeletedPublicationComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid publicationId)
    {
        await _publicationService.GetPublication(publicationId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, publicationId);
    }
}
