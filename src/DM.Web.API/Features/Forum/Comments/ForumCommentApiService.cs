using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Comments;

/// <inheritdoc />
internal class ForumCommentApiService : IForumCommentApiService
{
    private readonly ITopicCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ForumCommentApiService(
        ITopicCommentService commentService,
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
    public async Task<DiscussionResponse> GetDiscussion(Guid topicId, PagingQuery query)
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

        // Convert PagingQuery to CommentsQuery
        var commentsQuery = new CommentsQuery { Skip = query.Skip, Take = query.Take };
        var (comments, paging) = await _commentService.GetAsync(topicId, commentsQuery, excludeUserIds);
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
    public Task MarkAsRead(Guid topicId) => _commentService.MarkAsReadAsync(topicId);

    /// <inheritdoc />
    public Task MarkAsRead(string forumId) => _commentService.MarkBoardAsReadAsync(forumId);

    /// <inheritdoc />
    public Task MarkAllAsRead() => _commentService.MarkAllAsReadAsync();
}
