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
using DM.Domain.Game.Features.Blacklists;
using GameDto = DM.Domain.Game.Features.Games.Game;
using DM.Domain.Game.Tests.Dsl;
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
    private readonly IGameService gameService;
    private readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly IGameCommentRepository repository;
    private readonly IUnreadCountersRepository countersRepository;
    private readonly IEventProducer producer;
    private readonly GameCommentService service;
    private readonly Guid currentUserId;

    public GameCommentServiceShould()
    {
        var createValidator = Mock<IValidator<CreateComment>>();
        createValidator.ValidateAsync(Arg.Any<ValidationContext<CreateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateComment>>();
        updateValidator.ValidateAsync(Arg.Any<ValidationContext<UpdateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        gameService = Mock<IGameService>();
        intentionManager = Mock<IIntentionManager>();

        currentUserId = Guid.NewGuid();
        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        var guidFactory = Mock<IGuidFactory>();
        guidFactory.Create().Returns(Guid.NewGuid());

        repository = Mock<IGameCommentRepository>();
        countersRepository = Mock<IUnreadCountersRepository>();
        countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>()).Returns(Task.CompletedTask);

        producer = Mock<IEventProducer>();
        producer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        service = new GameCommentService(
            createValidator,
            updateValidator,
            gameService,
            intentionManager,
            identityProvider,
            dateTimeProvider,
            guidFactory,
            repository,
            countersRepository,
            producer);
    }

    /// <summary>
    /// The gate carries the same rule and would refuse in its generic words; the
    /// list is asked first, so the refusal says why.
    /// </summary>
    [Fact]
    public async Task RefuseABlacklistedUserInTheWordsOfTheList()
    {
        var gameId = Guid.NewGuid();
        gameService.GetAsync(gameId).Returns(BlacklistingGame(gameId, currentUserId));
        intentionManager
            .When(manager => manager.ThrowIfForbidden(GameIntention.CreateComment, Arg.Any<GameDto>()))
            .Do(_ => throw new IntentionManagerException(
                Identities.User(currentUserId).User, GameIntention.CreateComment));

        var act = async () => await service.CreateAsync(new CreateComment { EntityId = gameId, Text = "Test comment" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromGame);
        await repository.DidNotReceive().Create(Arg.Any<CreateGameCommentEntity>());
    }

    /// <summary>
    /// Rewriting one's comment publishes text exactly as writing it did.
    /// </summary>
    [Fact]
    public async Task RefuseABlacklistedAuthorTheEditOfHisComment()
    {
        var gameId = Guid.NewGuid();
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            EntityId = gameId,
            Text = "Original text",
            Author = new GeneralUser { UserId = currentUserId }
        };
        repository.Get(comment.Id).Returns(comment);
        gameService.GetAsync(gameId).Returns(BlacklistingGame(gameId, currentUserId));

        var act = async () => await service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden && e.Message == RefusalMessage.BlacklistedFromGame);
        await repository.DidNotReceive().Update(Arg.Any<UpdateGameCommentEntity>());
    }

    /// <summary>
    /// A moderator editing somebody else's comment is moderating: the author's
    /// standing in the game is not asked, and neither is the game.
    /// </summary>
    [Fact]
    public async Task LetAModeratorEditTheCommentOfABlacklistedAuthor()
    {
        var gameId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var comment = new Comment
        {
            Id = Guid.NewGuid(),
            EntityId = gameId,
            Text = "Original text",
            Author = new GeneralUser { UserId = authorId }
        };
        repository.Get(comment.Id).Returns(comment);
        repository.Update(Arg.Any<UpdateGameCommentEntity>()).Returns(comment);
        gameService.GetAsync(gameId).Returns(BlacklistingGame(gameId, authorId));

        await service.UpdateAsync(new UpdateComment { CommentId = comment.Id, Text = "Updated" });

        await repository.Received(1).Update(Arg.Any<UpdateGameCommentEntity>());
    }

    private static GameDto BlacklistingGame(Guid gameId, Guid userId) => new()
    {
        Id = gameId,
        Master = new GeneralUser { UserId = Guid.NewGuid(), Username = "Author" },
        BlacklistedUsers = [new BlacklistedUser { UserId = userId }]
    };

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var gameId = Guid.NewGuid();
        var createComment = new CreateComment { EntityId = gameId, Text = "Test comment" };
        var game = GameBuilder.Bare(gameId);
        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateGameCommentEntity>()).Returns(new Comment { Id = Guid.NewGuid() });

        await service.CreateAsync(createComment);

        intentionManager.Received(1).ThrowIfForbidden(GameIntention.CreateComment, game);
    }

    [Fact]
    public async Task CreateCommentAndPublishEvent()
    {
        var gameId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var createComment = new CreateComment { EntityId = gameId, Text = "Test comment" };
        var game = GameBuilder.Bare(gameId);
        gameService.GetAsync(gameId).Returns(game);
        repository.Create(Arg.Any<CreateGameCommentEntity>()).Returns(new Comment { Id = commentId });

        await service.CreateAsync(createComment);

        await producer.Received(1).SendAsync(EventType.NewGameComment, Arg.Any<Guid>());
    }

    /// <summary>
    /// The discussion is read by whoever may read the game: the read of the game
    /// is the whole question, and no intention narrows it after.
    /// </summary>
    [Fact]
    public async Task ReadTheDiscussionOfWhateverGameTheReaderMayOpen()
    {
        var gameId = Guid.NewGuid();
        var query = new CommentsQuery { Skip = 0 };
        var game = GameBuilder.Bare(gameId);
        gameService.GetAsync(gameId).Returns(game);
        repository.Count(gameId, Arg.Any<CommentsQuery>(), Arg.Any<IReadOnlyCollection<Guid>?>()).Returns(0);
        repository.Get(gameId, Arg.Any<CommentsQuery>(), Arg.Any<PagingData>(), Arg.Any<IReadOnlyCollection<Guid>?>())
            .Returns(Array.Empty<Comment>());

        await service.GetAsync(gameId, query);

        await gameService.Received(1).GetAsync(gameId);
        intentionManager.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated comment" };
        var comment = new Comment { Id = commentId, Text = "Original comment" };
        repository.Get(commentId).Returns(comment);
        repository.Update(Arg.Any<UpdateGameCommentEntity>()).Returns(comment);

        await service.UpdateAsync(updateComment);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
    }

    /// <summary>
    /// Reading and deleting a comment that is not there answer with the same
    /// code: the status is a property of the resource, not of the verb.
    /// </summary>
    [Fact]
    public async Task AnswerNotFoundForAMissingCommentOnBothReadAndDelete()
    {
        var commentId = Guid.NewGuid();
        repository.Get(commentId).Returns((Comment?)null);
        repository.GetForDelete(commentId).Returns((GameCommentToDelete?)null);

        var read = async () => await service.GetAsync(commentId);
        var delete = async () => await service.DeleteAsync(commentId);

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
        repository.GetForDelete(commentId).Returns(comment);
        DeleteGameCommentEntity? deleted = null;
        repository.Delete(Arg.Do<DeleteGameCommentEntity>(entity => deleted = entity))
            .Returns(true);

        await service.DeleteAsync(commentId);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
        // The author of the removal travels with it: Comment is ISoftDeletable and the
        // column stays empty unless the service hands the identity over.
        deleted!.DeletedByUserId.Should().Be(currentUserId);
        deleted.DeletedUtc.Should().NotBe(default);
    }

    /// <summary>
    /// The removal that went through pays the unread counters and announces itself.
    /// </summary>
    [Fact]
    public async Task PayTheCountersForTheRemovalThatWentThrough()
    {
        var commentId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var createdUtc = DateTimeOffset.UtcNow.AddHours(-2);
        repository.GetForDelete(commentId).Returns(new GameCommentToDelete
        {
            Id = commentId,
            EntityId = gameId,
            CreatedUtc = createdUtc
        });
        repository.Delete(Arg.Any<DeleteGameCommentEntity>()).Returns(true);

        await service.DeleteAsync(commentId);

        await countersRepository.Received(1).DecrementAsync(
            gameId, UnreadEntryType.Message, createdUtc);
        await producer.Received(1).SendAsync(EventType.DeletedGameComment, commentId);
    }

    /// <summary>
    /// A removal that found the comment already gone pays nothing.
    /// </summary>
    /// <remarks>
    /// The author and a moderator can both press delete, and the author can press
    /// it twice on a slow answer. The unread counter and the event belong to the
    /// removal that happened: repeating them here takes the counter down twice for
    /// one comment, and nothing recomputes it.
    /// </remarks>
    [Fact]
    public async Task LeaveTheCountersToTheRemovalThatTookTheComment()
    {
        var commentId = Guid.NewGuid();
        repository.GetForDelete(commentId).Returns(new GameCommentToDelete
        {
            Id = commentId,
            EntityId = Guid.NewGuid(),
            CreatedUtc = DateTimeOffset.UtcNow
        });
        repository.Delete(Arg.Any<DeleteGameCommentEntity>()).Returns(false);

        await service.DeleteAsync(commentId);

        await countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await producer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }
}
