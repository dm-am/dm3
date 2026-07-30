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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Likes;

public class GameCommentLikeServiceShould : UnitTestBase
{
    private readonly Mock<IGameCommentService> _commentService;
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ILikeOperations> _likeOperations;
    private readonly GameCommentLikeService _service;
    private readonly Guid _currentUserId;

    public GameCommentLikeServiceShould()
    {
        _commentService = Mock<IGameCommentService>();
        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CommentIntention>(), It.IsAny<Comment>()));

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _likeOperations = Mock<ILikeOperations>();
        _likeOperations.Setup(l => l.LikeAsync(It.IsAny<Comment>(), It.IsAny<EventType>()))
            .ReturnsAsync(new GeneralUser { UserId = _currentUserId });
        _likeOperations.Setup(l => l.UnlikeAsync(It.IsAny<Comment>()))
            .Returns(Task.CompletedTask);

        _service = new GameCommentLikeService(
            _commentService.Object,
            _gameService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _likeOperations.Object);
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
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        await _service.LikeCommentAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Like, comment), Times.Once);
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
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        await _service.LikeCommentAsync(commentId);

        _likeOperations.Verify(l => l.LikeAsync(comment, EventType.LikedGameComment), Times.Once);
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
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

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
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        await _service.UnlikeCommentAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Like, comment), Times.Once);
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
        _commentService.Setup(s => s.GetAsync(commentId)).ReturnsAsync(comment);
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        await _service.UnlikeCommentAsync(commentId);

        _likeOperations.Verify(l => l.UnlikeAsync(comment), Times.Once);
    }
}
