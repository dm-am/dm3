using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Likes;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Likes;
using DM.Testing;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Likes;

/// <summary>
/// A like is gated by the like intention of the comment and by nothing else. The
/// game's blacklist closes the discussion and the application, not likes, so the
/// service does not read the game at all; that a blacklisted reader likes like
/// anybody is held end to end by BlacklistRuleShould.
/// </summary>
public class GameCommentLikeServiceShould : UnitTestBase
{
    private readonly IGameCommentService _commentService;
    private readonly IIntentionManager _intentionManager;
    private readonly ILikeOperations _likeOperations;
    private readonly GameCommentLikeService _service;

    public GameCommentLikeServiceShould()
    {
        _commentService = Mock<IGameCommentService>();
        _intentionManager = Mock<IIntentionManager>();

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.LikeAsync(Arg.Any<Comment>(), Arg.Any<EventType>())
            .Returns(new GeneralUser { UserId = Guid.NewGuid() });
        _likeOperations.UnlikeAsync(Arg.Any<Comment>()).Returns(Task.CompletedTask);

        _service = new GameCommentLikeService(
            _commentService,
            _intentionManager,
            _likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeCommentAction()
    {
        var comment = GivenComment();

        await _service.LikeCommentAsync(comment.Id);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task LikeCommentUsingLikeOperations()
    {
        var comment = GivenComment();

        await _service.LikeCommentAsync(comment.Id);

        await _likeOperations.Received(1).LikeAsync(comment, EventType.LikedGameComment);
    }

    [Fact]
    public async Task AuthorizeUnlikeCommentAction()
    {
        var comment = GivenComment();

        await _service.UnlikeCommentAsync(comment.Id);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task UnlikeCommentUsingLikeOperations()
    {
        var comment = GivenComment();

        await _service.UnlikeCommentAsync(comment.Id);

        await _likeOperations.Received(1).UnlikeAsync(comment);
    }

    private Comment GivenComment()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        _commentService.GetAsync(comment.Id).Returns(comment);
        return comment;
    }
}
