using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Dto;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Creating;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Deleting;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Reading;
using DM.Services.Community.BusinessProcesses.Blogs.Comments.Updating;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Shared;
using Comment = DM.Web.API.Dto.Shared.Comment;

namespace DM.Web.API.Services.Blog;

/// <inheritdoc />
internal class PublicationCommentApiService : IPublicationCommentApiService
{
    private readonly ICommentReadingService _readingService;
    private readonly ICommentCreatingService _creatingService;
    private readonly ICommentUpdatingService _updatingService;
    private readonly ICommentDeletingService _deletingService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PublicationCommentApiService(
        ICommentReadingService readingService,
        ICommentCreatingService creatingService,
        ICommentUpdatingService updatingService,
        ICommentDeletingService deletingService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _readingService = readingService;
        _creatingService = creatingService;
        _updatingService = updatingService;
        _deletingService = deletingService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<DiscussionResponse> GetDiscussion(Guid publicationId, PagingQuery query)
    {
        var (comments, paging) = await _readingService.Get(publicationId, query);
        var identity = _identityProvider.Current;
        var currentUserId = identity.User?.UserId ?? Guid.Empty;
        var isAuthenticated = identity.User?.IsAuthenticated ?? false;
        var isModerator = (identity.User?.Role ?? UserRole.Guest) >= UserRole.Moderator;

        var discussionComments = comments.Select(c =>
        {
            var dc = _mapper.Map<DiscussionComment>(c);
            var isAuthor = c.Author?.UserId == currentUserId;
            dc.IsLikedByMe = c.Likes?.Any(l => l.UserId == currentUserId) ?? false;
            dc.CanEdit = isAuthor || isModerator;
            dc.CanDelete = isAuthor || isModerator;
            dc.CanLike = isAuthenticated && !isAuthor;
            return dc;
        }).ToList();

        var totalLikes = discussionComments.Sum(c => c.LikesCount);

        return new DiscussionResponse(
            discussionComments,
            new Paging(paging),
            totalLikes,
            isAuthenticated);
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Comment>> Get(Guid publicationId, PagingQuery query)
    {
        var (comments, paging) = await _readingService.Get(publicationId, query);
        return new ListEnvelope<Comment>(comments.Select(_mapper.Map<Comment>), new Paging(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid publicationId, Comment comment)
    {
        var createComment = _mapper.Map<CreateComment>(comment);
        createComment.EntityId = publicationId;
        var createdComment = await _creatingService.Create(createComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _readingService.Get(commentId);
        return new Envelope<Comment>(_mapper.Map<Comment>(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, Comment comment)
    {
        var updateComment = _mapper.Map<UpdateComment>(comment);
        updateComment.CommentId = commentId;
        var updatedComment = await _updatingService.Update(updateComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _deletingService.Delete(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid publicationId) => _readingService.MarkAsRead(publicationId);
}
