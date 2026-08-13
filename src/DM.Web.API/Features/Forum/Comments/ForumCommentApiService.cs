using System;
using DM.Domain.Core.Comments;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Dto;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Comments;
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
        var excludeUserIds = await CommentReading.HiddenAuthorsAsync(_blacklistChecker, identity);

        // Convert PagingQuery to CommentsQuery
        var commentsQuery = new CommentsQuery { Skip = query.Skip, Take = query.Take };
        var (comments, paging) = await _commentService.GetAsync(topicId, commentsQuery, excludeUserIds);

        return CommentReading.ToDiscussion(comments, paging, identity, _mapper);
    }

    /// <inheritdoc />
    public async Task<Envelope<FirstUnreadComment>> GetFirstUnread(string boardAlias, int topicNumber)
    {
        // The same authors the discussion itself hides: the position is counted
        // over the rows the reader is shown, so it has to know about them here.
        var hiddenAuthors = await CommentReading.HiddenAuthorsAsync(_blacklistChecker, _identityProvider.Current);
        var position = await _commentService.GetFirstUnreadAsync(boardAlias, topicNumber, hiddenAuthors);

        return new Envelope<FirstUnreadComment>(_mapper.Map<FirstUnreadComment>(position));
    }

    /// <inheritdoc />
    public Task MarkAsRead(Guid topicId) => _commentService.MarkAsReadAsync(topicId);

    /// <inheritdoc />
    public Task MarkAsRead(string forumId) => _commentService.MarkBoardAsReadAsync(forumId);

    /// <inheritdoc />
    public Task MarkAllAsRead() => _commentService.MarkAllAsReadAsync();
}
