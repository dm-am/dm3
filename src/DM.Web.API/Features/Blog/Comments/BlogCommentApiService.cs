using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Comment = DM.Web.API.Shared.Dto.Comment;
using DiscussionResponse = DM.Web.API.Shared.Dto.DiscussionResponse;
using CreateCommentRequest = DM.Web.API.Shared.Dto.CreateCommentRequest;

namespace DM.Web.API.Features.Blog.Comments;

/// <inheritdoc />
internal class BlogCommentApiService : IBlogCommentApiService
{
    private readonly IBlogCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BlogCommentApiService(
        IBlogCommentService commentService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker blacklistChecker,
        IMapper mapper)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _blacklistChecker = blacklistChecker;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<DiscussionResponse> GetDiscussion(Guid blogId, PagingQuery query)
    {
        var identity = _identityProvider.Current;
        var currentUserId = identity.User?.UserId ?? Guid.Empty;

        // Get blocked user IDs if HideComments setting is enabled
        IReadOnlyCollection<Guid>? excludeUserIds = null;
        if (identity.User?.IsAuthenticated == true)
        {
            var blockedIds = await _blacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                currentUserId, UserBlacklistSettings.HideComments);
            if (blockedIds.Count > 0)
            {
                excludeUserIds = blockedIds;
            }
        }

        var (comments, paging) = await _commentService.GetAsync(blogId, query, excludeUserIds);
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
    public async Task<ListEnvelope<Comment>> Get(Guid blogId, PagingQuery query)
    {
        var identity = _identityProvider.Current;
        IReadOnlyCollection<Guid>? excludeUserIds = null;
        if (identity.User?.IsAuthenticated == true)
        {
            var blockedIds = await _blacklistChecker.GetBlockedUserIdsIfFlagEnabledAsync(
                identity.User.UserId, UserBlacklistSettings.HideComments);
            if (blockedIds.Count > 0)
            {
                excludeUserIds = blockedIds;
            }
        }

        var (comments, paging) = await _commentService.GetAsync(blogId, query, excludeUserIds);
        return new ListEnvelope<Comment>(comments.Select(_mapper.Map<Comment>), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Create(Guid blogId, CreateCommentRequest request)
    {
        var createComment = _mapper.Map<CreateComment>(request);
        createComment.EntityId = blogId;
        var createdComment = await _commentService.CreateAsync(createComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(createdComment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Get(Guid commentId)
    {
        var comment = await _commentService.GetAsync(commentId);
        return new Envelope<Comment>(_mapper.Map<Comment>(comment));
    }

    /// <inheritdoc />
    public async Task<Envelope<Comment>> Update(Guid commentId, Comment comment)
    {
        var updateComment = _mapper.Map<UpdateComment>(comment);
        updateComment.CommentId = commentId;
        var updatedComment = await _commentService.UpdateAsync(updateComment);
        return new Envelope<Comment>(_mapper.Map<Comment>(updatedComment));
    }

    /// <inheritdoc />
    public Task Delete(Guid commentId) => _commentService.DeleteAsync(commentId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid blogId) => _commentService.MarkAsReadAsync(blogId);
}
