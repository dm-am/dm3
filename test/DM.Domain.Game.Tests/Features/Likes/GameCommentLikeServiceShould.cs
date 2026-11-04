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
    private readonly IGameCommentService commentService;
    private readonly IIntentionManager intentionManager;
    private readonly ILikeOperations likeOperations;
    private readonly GameCommentLikeService service;

    public GameCommentLikeServiceShould()
    {
        commentService = Mock<IGameCommentService>();
        intentionManager = Mock<IIntentionManager>();

        likeOperations = Mock<ILikeOperations>();
        likeOperations.LikeAsync(Arg.Any<Comment>(), Arg.Any<EventType>())
            .Returns(new GeneralUser { UserId = Guid.NewGuid() });
        likeOperations.UnlikeAsync(Arg.Any<Comment>()).Returns(Task.CompletedTask);

        service = new GameCommentLikeService(
            commentService,
            intentionManager,
            likeOperations);
    }

    [Fact]
    public async Task LikeACommentTheCallerMayLike()
    {
        var comment = GivenComment();

        await service.LikeCommentAsync(comment.Id);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await likeOperations.Received(1).LikeAsync(comment, EventType.LikedGameComment);
    }

    [Fact]
    public async Task UnlikeACommentTheCallerMayLike()
    {
        var comment = GivenComment();

        await service.UnlikeCommentAsync(comment.Id);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
        await likeOperations.Received(1).UnlikeAsync(comment);
    }

    private Comment GivenComment()
    {
        var comment = new Comment { Id = Guid.NewGuid(), EntityId = Guid.NewGuid() };
        commentService.GetAsync(comment.Id).Returns(comment);
        return comment;
    }
}
