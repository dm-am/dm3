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
    private readonly IBbTagNameCase _tagNames;

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
        IEventProducer invokedEventProducer,
        IBbTagNameCase tagNames)
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
        _tagNames = tagNames;
    }

    /// <inheritdoc />
    public async Task<Comment> CreateAsync(CreateComment createComment)
    {
        await _createValidator.ValidateAndThrowAsync(createComment);

        var publication = await _publicationService.GetPublication(createComment.EntityId);
        var blog = await _blogService.GetBlogAsync(publication.BlogId);
        var currentUser = _identityProvider.Current.User;

        // The blog's blacklist covers the comments under its publications, and
        // is asked before the gate, the way the blog's own discussion asks it.
        // The three refusals below are the three conditions of MayCreate, one
        // for one and in its order: whatever is added to one side goes to the
        // other, or the page offers a form this method refuses.
        blog.ThrowIfBlacklisted(currentUser.UserId);
        _intentionManager.ThrowIfForbidden(PublicationIntention.CreateComment, publication);

        // Ban check lives here rather than in PublicationIntentionResolver: the
        // rule is about whose blog this is, and the publication carries no blog
        // ownership. The blog is loaded above anyway. The rule itself is not
        // duplicated: BlogRoleExtensions.IsOwnBlog owns "whose blog this is", and
        // AccessRestrictions owns what a ban does with the answer, refusal
        // included.
        currentUser.ThrowIfMayNotComment(inOwnSpace: blog.IsOwnBlog(currentUser.UserId));

        // Strip [mod] authored by a non-moderator (it renders as a green mod
        // block on the Comment surface); Moderator+ may author it.
        createComment.Text = _tagNames.ToLowerCase(ModBlockSanitizer.SanitizeForAuthor(createComment.Text, currentUser.Role), BbTagSet.WithModBlock);

        var (createdComment, commentId) = await _repository.Create(
            createComment,
            currentUser.UserId,
            publication.Id);

        await _countersRepository.IncrementAsync(publication.Id, UnreadEntryType.Message);
        // The publication event, not the blog one: a comment carries the id of
        // what it hangs on, so the blog generator joined Comments to Blogs on a
        // publication id, matched nothing and produced no notification. Nothing
        // failed — the author of a commented publication simply was never told.
        await _invokedEventProducer.SendAsync(EventType.NewPublicationComment, commentId);

        return createdComment;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Reads the blog the way CreateAsync reads it: the blacklist and whose blog
    /// this is are the blog's, and the publication carries neither.
    /// </remarks>
    public async Task<bool> MayCreateAsync(Publication publication)
    {
        var blog = await _blogService.GetBlogAsync(publication.BlogId);
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
        var paging = new PagingData(query, _identityProvider.Current.Settings.Paging.CommentsPerPage, totalCount);

        var comments = await _repository.Get(publicationId, query, paging, excludeUserIds);

        return (comments, paging.Result);
    }

    /// <inheritdoc />
    public async Task<Comment> GetAsync(Guid commentId)
    {
        var comment = await FindAsync(commentId);

        // The four discussions share one table and the row above is read by its
        // id alone, so the discussion is asked here exactly as the listing asks it.
        // A discussion that is not there to the reader - hidden, removed, or of
        // another kind, which this route has nothing to check by - answers exactly
        // as a comment that is not there: a different answer would tell a
        // stranger that the id resolves, and to what.
        await DiscussionRefusal.AsCommentNotFound(commentId,
            () => _publicationService.GetPublication(comment.EntityId));

        return comment;
    }

    /// <summary>
    /// The comment by its identifier, among the comments of publications only
    /// (the repository narrows it: a lookup by id alone let this route edit a
    /// comment of another discussion past that discussion's rules). The edit
    /// keeps to this lookup: who may edit is CommentIntention.Edit's answer on
    /// the row.
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
        // blacklist and the ban are asked here too, on the same terms as in
        // CreateAsync. Only the author is asked, a moderator editing somebody
        // else's comment is moderating and neither list takes a moderator tool
        // away.
        var currentUser = _identityProvider.Current.User;
        if (comment.Author?.UserId == currentUser.UserId)
        {
            var publication = await DiscussionRefusal.AsCommentNotFound(updateComment.CommentId,
                () => _publicationService.GetPublication(comment.EntityId));
            var blog = await _blogService.GetBlogAsync(publication.BlogId);
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
            // Somebody else removed this comment between the read above and the
            // write. The publication's unread counter and the event belong to the
            // removal that happened, and repeating them here takes the counter down
            // twice for one comment. The caller is answered the same either way:
            // the comment is gone, which is what they asked for.
            return;
        }

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
