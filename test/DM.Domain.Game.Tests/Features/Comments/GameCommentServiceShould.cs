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
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.Comments;

public class GameCommentServiceShould : UnitTestBase
{
    private readonly IGameService _gameService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IGameCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _producer;
    private readonly GameCommentService _service;
    private readonly Guid _currentUserId;

    public GameCommentServiceShould()
    {
        var createValidator = Mock<IValidator<CreateComment>>();
        createValidator.ValidateAsync(Arg.Any<ValidationContext<CreateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateComment>>();
        updateValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _gameService = Mock<IGameService>();
        _intentionManager = Mock<IIntentionManager>();

        _currentUserId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        _repository = Mock<IGameCommentRepository>();
        _countersRepository = Mock<IUnreadCountersRepository>();
        _countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>()).Returns(Task.CompletedTask);

        _producer = Mock<IEventProducer>();
        _producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _service = new GameCommentService(
            createValidator,
            updateValidator,
            _gameService,
            _intentionManager,
            _identityProvider,
            dateTimeProvider,
            guidFactory,
            _repository,
            _countersRepository,
            _producer);
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
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateGameCommentEntity>()).Returns(new Comment { Id = Guid.NewGuid() });

        await _service.CreateAsync(createComment);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.CreateComment, game);
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
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Create(Arg.Any<CreateGameCommentEntity>()).Returns(new Comment { Id = commentId });

        await _service.CreateAsync(createComment);

        await _producer.Received(1).SendAsync(EventType.NewGameComment, Arg.Any<Guid>());
    }

    [Fact]
    public async Task AuthorizeReadCommentsAction()
    {
        var gameId = Guid.NewGuid();
        var query = new CommentsQuery { Skip = 0 };
        var game = new GameDto
        {
            Id = gameId,
            Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" }
        };
        _gameService.GetAsync(gameId).Returns(game);
        _repository.Count(gameId, Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>()).Returns(0);
        _repository.Get(gameId, Arg.Any<CommentsQuery>(), Arg.Any<PagingData>(), Arg.Any<IReadOnlyCollection<Guid>?>())
            .Returns(Array.Empty<Comment>());

        await _service.GetAsync(gameId, query);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.ReadComments, game);
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated comment" };
        var comment = new Comment { Id = commentId, Text = "Original comment" };
        _repository.Get(commentId).Returns(comment);
        _repository.Update(Arg.Any<UpdateGameCommentEntity>()).Returns(comment);

        await _service.UpdateAsync(updateComment);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
    }

    /// <summary>
    /// Reading and deleting a comment that is not there answer with the same
    /// code. They used to disagree inside this one service: Gone on the read and
    /// NotFound on the delete, which made the status a property of the verb
    /// instead of a property of the resource.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundForAMissingCommentOnBothReadAndDelete()
    {
        var commentId = Guid.NewGuid();
        _repository.Get(commentId).Returns((Comment?)null);
        _repository.GetForDelete(commentId).Returns((GameCommentToDelete?)null);

        var read = async () => await _service.GetAsync(commentId);
        var delete = async () => await _service.DeleteAsync(commentId);

        await read.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
        await delete.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeDeleteCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new GameCommentToDelete { Id = commentId, EntityId = Guid.NewGuid() };
        _repository.GetForDelete(commentId).Returns(comment);
        DeleteGameCommentEntity? deleted = null;
        _repository.Delete(Arg.Any<DeleteGameCommentEntity>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var entity = ci.ArgAt<DeleteGameCommentEntity>(0);
                deleted = entity;
            });

        await _service.DeleteAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
        // The author of the removal travels with it: Comment is ISoftDeletable and the
        // column stays empty unless the service hands the identity over.
        deleted!.DeletedByUserId.Should().Be(_currentUserId);
        deleted.DeletedUtc.Should().NotBe(default);
    }
}
