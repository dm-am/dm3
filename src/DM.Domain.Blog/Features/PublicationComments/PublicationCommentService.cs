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
    private readonly IEventProducer _eventProducer;

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
        IEventProducer eventProducer)
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
        _eventProducer = eventProducer;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var publication = await _publicationService.GetPublication(createComment.EntityId);
        var blog = await _blogService.GetUnfiltered(publication.BlogId);
        var currentUser = _identityProvider.Current.User;

        // The three refusals below are MayCreateAsync's three conditions in its
        // order: whatever is added to one side goes to the other.
        blog.ThrowIfBlacklisted(currentUser.UserId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.CreateComment, publication);

        // Here and not in the resolver: the ban asks whose blog this is, and the publication carries no ownership.
        currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));

        // [mod] renders as a mod block on the Comment surface; Moderator+ may author it.
        createComment.Text = ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role);

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUser.UserId,
            publication.Id);

        await _countersRepository.IncrementAsync(publication.Id, UnreadEntryType.Message);
        // The publication event, not the blog one: the comment carries the publication's id.
        await _eventProducer.SendAsync(EventType.NewPublicationComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the blog the way CreateAsync reads it: the blacklist and whose blog
    /// this is are the blog's, and the publication carries neither.
    /// </remarks>
    public async Task<bool> MayCreateAsync(Publication publication)
    {
        var blog = await _blogService.GetUnfiltered(publication.BlogId);
        var currentUser = _identityProvider.Current.User;

        // CreateAsync's three refusals, asked as questions in the same order.
        return !blog.IsBlacklisted(currentUser.UserId) &&
               _intentionManager.IsAllowed(PublicationIntention.CreateComment, publication) &&
               currentUser.MaySpeak(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<Comment> Comments, PagingResult Paging)> GetAsync(Guid publicationId, CommentsQuery query,
        IReadOnlyCollection<Guid>? excludeUserIds = null)
    {
        await _publicationService.GetPublication(publicationId);

        var totalCount = await _repository.Count(publicationId, query, excludeUserIds);
        var paging = new PagingData(query, _identityProvider.Current.Settings.PageSize, totalCount);

        var comments = await _repository.Get(publicationId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // The discussions share one table: a comment of a publication not there to the
        // reader answers as one that is not there, or the id would tell a stranger what it resolves to.
        await DiscussionRefusal.AsCommentNotFound(commentId,
            () => _publicationService.GetPublication(comment.EntityId));

        return comment;
    }

    /// <summary>
    /// The comment by its identifier, among the comments of publications only, so
    /// this route cannot edit a comment of another discussion past its rules.
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

        // Rewriting publishes text as writing does, so the author is asked the
        // blacklist and the ban too; a moderator editing somebody else's comment is moderating.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            var publication = await DiscussionRefusal.AsCommentNotFound(updateComment.CommentId,
                () => _publicationService.GetPublication(comment.EntityId));
            var blog = await _blogService.GetUnfiltered(publication.BlogId);
            blog.ThrowIfBlacklisted(currentUser.UserId);
            if (!currentUser.MaySpeak())
            {
                currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));
            }
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

        var entity = new UpdatePublicationCommentEntity
        {
            CommentId = updateComment.CommentId,
            Text = text,
            LastUpdateUtc = _dateTimeProvider.Now,
            EditorUserId = currentUser.UserId
        };

        var updatedComment = await _repository.Update(entity);
        await _eventProducer.SendAsync(EventType.ChangedPublicationComment, updateComment.CommentId);
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

        var entity = new DeletePublicationCommentEntity
        {
            CommentId = commentId,
            PublicationId = comment.PublicationId,
            DeletedByUserId = _identityProvider.Current.User.UserId,
            DeletedUtc = _dateTimeProvider.Now
        };

        // The publication's comment count and its last-comment pointer move inside
        // Delete, in the transaction that marks the comment.
        var removed = await _repository.Delete(entity);
        if (!removed)
        {
            // Somebody else removed it in between; the counter and the event are that removal's.
            return;
        }

        await _countersRepository.DecrementAsync(comment.PublicationId, UnreadEntryType.Message, comment.CreatedUtc);

        await _eventProducer.SendAsync(EventType.DeletedPublicationComment, commentId);
    }

    /// <inheritdoc />
    public async Task MarkAsReadAsync(Guid publicationId)
    {
        await _publicationService.GetPublication(publicationId);
        await _countersRepository.FlushAsync(_identityProvider.Current.User.UserId,
            UnreadEntryType.Message, publicationId);
    }
}
