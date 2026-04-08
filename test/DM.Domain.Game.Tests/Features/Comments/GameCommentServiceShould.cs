using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Games;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Tests.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Comments;

public class GameCommentServiceShould : UnitTestBase
{
    private readonly Mock<IGameService> _gameService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IGameCommentRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _producer;
    private readonly GameCommentService _service;
    private readonly Guid _currentUserId;

    public GameCommentServiceShould()
    {
        var createValidator = Mock<IValidator<CreateComment>>();
        createValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateComment>>();
        updateValidator.Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>(), It.IsAny<GameDto>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CommentIntention>(), It.IsAny<Comment>()));

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(_currentUserId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Setup(g => g.Create()).Returns(Guid.NewGuid());

        _repository = Mock<IGameCommentRepository>();
        _countersRepository = Mock<IUnreadCountersRepository>();
        _countersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);

        _producer = Mock<IEventProducer>();
        _producer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>())).Returns(Task.CompletedTask);

        _service = new GameCommentService(
            createValidator.Object,
            updateValidator.Object,
            _gameService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            dateTimeProvider.Object,
            guidFactory.Object,
            _repository.Object,
            _countersRepository.Object,
            _producer.Object);
    }

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var gameId = Guid.NewGuid();
        var createComment = new CreateComment { EntityId = gameId, Text = "Test comment" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = Guid.NewGuid() });

        await _service.CreateAsync(createComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.CreateComment, game), Times.Once);
    }

    [Fact]
    public async Task ThrowForbiddenWhenUserIsBlacklistedFromGame()
    {
        var gameId = Guid.NewGuid();
        var createComment = new CreateComment { EntityId = gameId, Text = "Test comment" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = new[] { new BlacklistedUser { UserId = _currentUserId } }
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);

        var act = async () => await _service.CreateAsync(createComment);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCommentAndPublishEvent()
    {
        var gameId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var createComment = new CreateComment { EntityId = gameId, Text = "Test comment" };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
            BlacklistedUsers = Array.Empty<BlacklistedUser>()
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Create(It.IsAny<CreateGameCommentEntity>()))
            .ReturnsAsync(new Comment { Id = commentId });

        await _service.CreateAsync(createComment);

        _producer.Verify(p => p.SendAsync(EventType.NewGameComment, It.IsAny<Guid>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeReadCommentsAction()
    {
        var gameId = Guid.NewGuid();
        var query = new GameCommentsQuery { Skip = 0 };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        _gameService.Setup(s => s.GetAsync(gameId)).ReturnsAsync(game);
        _repository.Setup(r => r.Count(gameId, It.IsAny<GameCommentsQuery>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .ReturnsAsync(0);
        _repository.Setup(r => r.Get(gameId, It.IsAny<GameCommentsQuery>(), It.IsAny<PagingData>(), It.IsAny<IReadOnlyCollection<Guid>?>()))
            .ReturnsAsync(Array.Empty<Comment>());

        await _service.GetAsync(gameId, query);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.ReadComments, game), Times.Once);
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated comment" };
        var comment = new Comment { Id = commentId, Text = "Original comment" };
        _repository.Setup(r => r.Get(commentId)).ReturnsAsync(comment);
        _repository.Setup(r => r.Update(It.IsAny<UpdateGameCommentEntity>()))
            .ReturnsAsync(comment);

        await _service.UpdateAsync(updateComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Edit, comment), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new GameCommentToDelete { Id = commentId, GameId = Guid.NewGuid() };
        _repository.Setup(r => r.GetForDelete(commentId)).ReturnsAsync(comment);
        _repository.Setup(r => r.Delete(It.IsAny<DeleteGameCommentEntity>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Delete, It.IsAny<Comment>()), Times.Once);
    }
}
