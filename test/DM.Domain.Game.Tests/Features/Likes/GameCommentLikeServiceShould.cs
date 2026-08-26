using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Likes;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Features.Likes;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Likes;

public class GameCommentLikeServiceShould : UnitTestBase
{
    private readonly IGameCommentService _commentService;
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly ILikeOperations _likeOperations;
    private readonly GameCommentLikeService _service;
    private readonly Guid _currentUserId;

    public GameCommentLikeServiceShould()
    {
        _commentService = Mock<IGameCommentService>();
        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.LikeAsync(Arg.Any<Comment>(), Arg.Any<EventType>())
            .Returns(new GeneralUser { UserId = _currentUserId });
        _likeOperations.UnlikeAsync(Arg.Any<Comment>()).Returns(Task.CompletedTask);

        _service = new GameCommentLikeService(
            _commentService,
            _gameService,
            _intentionManager,
            _identityProvider,
            _likeOperations);
    }

    [Fact]
    public async Task AuthorizeLikeCommentAction()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = gameId };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _gameService.GetAsync(gameId).Returns(game);

        await _service.LikeCommentAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task LikeCommentUsingLikeOperations()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = gameId };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _gameService.GetAsync(gameId).Returns(game);

        await _service.LikeCommentAsync(commentId);

        await _likeOperations.Received(1).LikeAsync(comment, EventType.LikedGameComment);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserIsBlacklistedFromGame()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = gameId };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = _currentUserId } }
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _gameService.GetAsync(gameId).Returns(game);

        var act = async () => await _service.LikeCommentAsync(commentId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuthorizeUnlikeCommentAction()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = gameId };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _gameService.GetAsync(gameId).Returns(game);

        await _service.UnlikeCommentAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Like, comment);
    }

    [Fact]
    public async Task UnlikeCommentUsingLikeOperations()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, EntityId = gameId };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _commentService.GetAsync(commentId).Returns(comment);
        _gameService.GetAsync(gameId).Returns(game);

        await _service.UnlikeCommentAsync(commentId);

        await _likeOperations.Received(1).UnlikeAsync(comment);
    }
}
