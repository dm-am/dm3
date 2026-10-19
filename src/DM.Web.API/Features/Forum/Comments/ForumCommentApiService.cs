using System;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Blacklists;
using DM.Domain.Forum.Features.Comments;
using DM.Web.API.Shared.Comments;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Comments;

public class ForumCommentApiService
{
    private readonly ITopicCommentService _commentService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly ForumCommentMapper _forumCommentMapper;

    public ForumCommentApiService(
        ITopicCommentService commentService,
        IIdentityProvider identityProvider,
        IUserBlacklistChecker blacklistChecker,
        ForumCommentMapper forumCommentMapper)
    {
        _commentService = commentService;
        _identityProvider = identityProvider;
        _blacklistChecker = blacklistChecker;
        _forumCommentMapper = forumCommentMapper;
    }

    public async Task<Envelope<FirstUnreadComment>> GetFirstUnread(string boardAlias, int topicNumber)
    {
        // The same authors the discussion itself hides: the position is counted
        // over the rows the reader is shown, so it has to know about them here.
        var hiddenAuthors = await CommentReading.HiddenAuthorsAsync(_blacklistChecker, _identityProvider.Current);
        var position = await _commentService.GetFirstUnreadAsync(boardAlias, topicNumber, hiddenAuthors);

        return new Envelope<FirstUnreadComment>(_forumCommentMapper.ToFirstUnreadComment(position));
    }

    public Task MarkTopicAsRead(Guid topicId) => _commentService.MarkAsReadAsync(topicId);

    public Task MarkBoardAsRead(string forumId) => _commentService.MarkBoardAsReadAsync(forumId);

    public Task MarkAllAsRead() => _commentService.MarkAllAsReadAsync();
}
