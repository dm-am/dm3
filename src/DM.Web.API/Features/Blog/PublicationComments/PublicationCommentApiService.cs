using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.PublicationComments;

/// <inheritdoc />
internal class PublicationCommentApiService : IPublicationCommentApiService
{
    private readonly IPublicationCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PublicationCommentApiService(
        IPublicationCommentService commentService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<DiscussionResponse> GetDiscussion(Guid publicationId, PagingQuery query)
    {
        var (comments, paging) = await _commentService.Get(publicationId, query);
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
        var (comments, paging) = await _commentService.Get(publicationId, query);
        return new ListEnvelope<Comment>(comments.Select(_mapper.Map<Comment>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid publicationId, CreateCommentRequest request)
    {
        var createComment = _mapper.Map<CreateComment>(request);
        createComment.EntityId = publicationId;
        var createdComment = await _commentService.Create(createComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _commentService.Get(commentId);
        return new Envelope<Comment>(_mapper.Map<Comment>(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, Comment comment)
    {
        var updateComment = _mapper.Map<UpdateComment>(comment);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.Update(updateComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _commentService.Delete(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid publicationId) => _commentService.MarkAsRead(publicationId);
}
