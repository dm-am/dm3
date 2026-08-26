using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Caching;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Core.Users;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class TopicServiceShould : UnitTestBase
{
    private readonly IBoardService _boardService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly IAccessPolicyConverter _accessPolicyConverter;
    private readonly ITopicRepository _repository;
    private readonly IGuidFactory _guidFactory;
    private readonly IUnreadCountersRepository _unreadCountersRepository;
    private readonly IUserLookupService _userLookupService;
    private readonly IEventProducer _eventProducer;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly TopicService _service;

    public TopicServiceShould()
    {
        var createValidator = Mock<IValidator<CreateTopic>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateTopic>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateTopic>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateTopic>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _boardService = Mock<IBoardService>();

        _accessPolicyConverter = Mock<IAccessPolicyConverter>();
        _accessPolicyConverter.Convert(Arg.Any<UserRole>()).Returns(BoardAccessPolicy.Guest);

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.IsAllowed(Arg.Any<ForumIntention>(), Arg.Any<Board>()).Returns(false);

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _repository = Mock<ITopicRepository>();
        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Create().Returns(_ => Guid.NewGuid());

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.CreateMarkerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<UnreadEntryType>())
            .Returns(Task.CompletedTask);
        _unreadCountersRepository.SelectByEntitiesAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<Guid[]>())
            .Returns(ci =>
            {
                var userId = ci.ArgAt<Guid>(0);
                var type = ci.ArgAt<UnreadEntryType>(1);
                var ids = ci.ArgAt<Guid[]>(2);
                return ids.ToDictionary(id => id, _ => 0);
            });

        _userLookupService = Mock<IUserLookupService>();

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        // Pass-through cache: always delegates to the factory so the
        // service's cacheable fast path still calls the repository in
        // tests. We don't want to test the cache here, only the
        // underlying read logic.
        var cache = Mock<ICache>();
        cache
            .GetOrCreateAsync(
                Arg.Any<object>(),
                Arg.Any<Func<Task<Topic[]>>>(),
                Arg.Any<TimeSpan>())
            .Returns(ci => ci.ArgAt<Func<Task<Topic[]>>>(1)());

        _service = new TopicService(
            createValidator,
            updateValidator,
            _boardService,
            _accessPolicyConverter,
            _intentionManager,
            _identityProvider,
            _repository,
            _unreadCountersRepository,
            _userLookupService,
            _eventProducer,
            _guidFactory,
            cache);
    }

    /// <summary>The store answers a creation with this topic.</summary>
    private void CreateReturns(Topic topic) =>
        _repository.Create(
            Arg.Any<CreateTopicEntity>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>()).Returns(topic);

    /// <summary>The store refuses a creation with this failure.</summary>
    private void CreateThrows(Exception failure) =>
        _repository.Create(
            Arg.Any<CreateTopicEntity>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>()).ThrowsAsync(failure);

    [Fact]
    public async Task AuthorizeCreateTopicAction()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.GetBoard("General", true).Returns(board);
        CreateReturns(new Topic());

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.CreateAsync(createTopic);

        _intentionManager.Received(1).ThrowIfForbidden(ForumIntention.CreateTopic, board);
    }

    [Fact]
    public async Task CreateTopicAndInitializeUnreadCounters()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.GetBoard("General", true).Returns(board);

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        _guidFactory.Create().Returns(topicId);

        var expectedTopic = new Topic { Id = topicId };
        CreateReturns(expectedTopic);

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        var result = await _service.CreateAsync(createTopic);

        result.Should().Be(expectedTopic);
        await _unreadCountersRepository.Received(1).CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message);
        // The row landed, so the reservation was committed.
        await _unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>());
    }

    [Fact]
    public async Task TakeTheUnreadMarkerBackWhenTheTopicRowDoesNotLand()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _boardService.GetBoard("General", true).Returns(new Board { Id = boardId, Title = "General" });
        _guidFactory.Create().Returns(topicId);
        CreateThrows(new InvalidOperationException("storage refused"));

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.Awaiting(s => s.CreateAsync(createTopic))
            .Should().ThrowAsync<InvalidOperationException>(
                "the refusal of the store is what the caller has to see");

        // Written first and taken back, so the failure loses a topic nobody has
        // seen. The other order left a committed topic whose counters never exist.
        await _unreadCountersRepository.Received(1).CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message);
        await _unreadCountersRepository.Received(1).DeleteAsync(topicId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task PublishNewTopicEvent()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.GetBoard("General", true).Returns(board);
        CreateReturns(new Topic { Id = topicId });

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.CreateAsync(createTopic);

        await _eventProducer.Received(1).SendAsync(EventType.NewTopic, topicId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTopicNeverExisted()
    {
        var topicId = Guid.NewGuid();
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns((Topic)null!);
        _repository.Exists(topicId, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await _service.GetAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain("Топик не найден");
    }

    [Fact]
    public async Task ThrowGoneWhenTopicWasRemoved()
    {
        var topicId = Guid.NewGuid();
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns((Topic)null!);
        _repository.Exists(topicId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await _service.GetAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task AuthorizeUpdateTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await _service.UpdateAsync(updateTopic);

        _intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Edit, topic);
    }

    [Fact]
    public async Task PublishChangedTopicEvent()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await _service.UpdateAsync(updateTopic);

        await _eventProducer.Received(1).SendAsync(EventType.ChangedTopic, topicId);
    }

    /// <summary>
    /// A save that changed nothing announces nothing.
    /// </summary>
    /// <remarks>
    /// The two halves have to agree. The repository writes an edit-history row only
    /// when the tracker says the row moved, and the ChangedTopic notification reads
    /// its actor back from that history — so an event sent for a save that left no
    /// row names whoever edited the topic last. A moderator edits, the author reopens
    /// the form and saves it unchanged, and the subscribers get an announcement
    /// attributed to the moderator: filtered against his blacklist, and not against
    /// the blacklist of the person who actually pressed save.
    /// </remarks>
    [Fact]
    public async Task SendNoChangedTopicEventWhenTheUpdateChangedNothing()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, false));

        var result = await _service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Same Title" });

        result.Should().BeSameAs(topic, "the request is answered either way — it is not an error");
        await _eventProducer.DidNotReceive().SendAsync(EventType.ChangedTopic, Arg.Any<Guid>());
    }

    /// <summary>
    /// The editor travels to the repository, which is what makes the topic edit
    /// history hold anybody at all.
    /// </summary>
    /// <remarks>
    /// The topic row keeps its author and no editor, so the only record of who
    /// changed a topic is the history the repository writes from this field.
    /// Left unset it defaults to Guid.Empty, the repository skips the write, and
    /// the ChangedTopic notification goes out with no actor — meaning a topic
    /// edited by somebody the subscriber has blocked is still delivered.
    /// Asserted against the author too: the two are different people whenever a
    /// moderator edits, and taking one for the other filters the wrong person.
    /// </remarks>
    [Fact]
    public async Task RecordTheCurrentUserAsTheEditorOfTheTopic()
    {
        var topicId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Author = new GeneralUser { UserId = authorId },
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        UpdateTopicEntity? passed = null;
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>())
            .Returns(new TopicUpdateResult(topic, true))
            .AndDoes(ci =>
            {
                var entity = ci.ArgAt<UpdateTopicEntity>(0);
                passed = entity;
            });

        await _service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Updated Title" });

        passed.Should().NotBeNull();
        passed!.EditorUserId.Should().Be(_currentUserId,
            "the history is the only place a topic keeps who changed it");
        passed.EditorUserId.Should().NotBe(authorId,
            "the person editing is not necessarily the person who opened the topic");
    }

    [Fact]
    public async Task AllowAdminToChangeBoard()
    {
        var topicId = Guid.NewGuid();
        var oldBoardId = Guid.NewGuid();
        var newBoardId = Guid.NewGuid();
        var oldBoard = new Board { Id = oldBoardId, Title = "OldBoard" };
        var newBoard = new Board { Id = newBoardId, Title = "NewBoard" };
        var topic = new Topic { Id = topicId, Board = oldBoard };

        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), newBoardId).Returns(new TopicUpdateResult(topic, true));

        _intentionManager.IsAllowed(ForumIntention.AdministrateTopics, oldBoard).Returns(true);
        _boardService.GetBoard("NewBoard", false).Returns(newBoard);

        var updateTopic = new UpdateTopic { TopicId = topicId, BoardTitle = "NewBoard" };
        await _service.UpdateAsync(updateTopic);

        await _unreadCountersRepository.Received(1).ChangeParentAsync(oldBoardId, UnreadEntryType.Message, newBoardId);
        await _repository.Received(1).Update(Arg.Any<UpdateTopicEntity>(), newBoardId);
    }

    [Fact]
    public async Task AuthorizeDeleteTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Delete(topicId, _currentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(topicId);

        _intentionManager.Received(1).ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Board);
    }

    [Fact]
    public async Task DeleteTopicAndCleanupUnreadCounters()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic
        {
            Id = topicId,
            Board = new Board { Id = Guid.NewGuid(), Title = "General" }
        };
        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Delete(topicId, _currentUserId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(topicId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await _repository.Received(1).Delete(topicId, _currentUserId);
        await _unreadCountersRepository.Received(1).DeleteAsync(topicId, UnreadEntryType.Message);
        await _eventProducer.Received(1).SendAsync(EventType.DeletedTopic, topicId);
    }

    /// <summary>
    /// Closing or pinning somebody else's topic is refused, not dropped. Both
    /// flags used to be set to null when the caller could not administrate the
    /// board, so the request came back 200 with an open topic - the same answer a
    /// successful close produces.
    /// </summary>
    [Theory]
    [InlineData(true, null)]
    [InlineData(null, true)]
    public async Task RefuseTheAdminFlagsTheCallerMayNotSet(bool? isClosed, bool? isAttached)
    {
        var topicId = Guid.NewGuid();
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var topic = new Topic { Id = topicId, Board = board, IsClosed = false, IsAttached = false };

        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));
        _intentionManager.IsAllowed(ForumIntention.AdministrateTopics, board).Returns(false);
        _intentionManager
            .When(m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = isClosed,
            IsAttached = isAttached
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused change writes nothing.
        await _repository.DidNotReceive().Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>());
    }

    /// <summary>
    /// The client round-trips the whole topic, so the flags it already carries are
    /// nobody's attempt at anything and must not be refused.
    /// </summary>
    [Fact]
    public async Task NotRefuseTheAdminFlagsTheTopicAlreadyCarries()
    {
        var topicId = Guid.NewGuid();
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var topic = new Topic { Id = topicId, Board = board, IsClosed = true, IsAttached = false };

        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));
        _intentionManager.IsAllowed(ForumIntention.AdministrateTopics, board).Returns(false);

        await _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = true,
            IsAttached = false
        });

        _intentionManager.DidNotReceive().ThrowIfForbidden(ForumIntention.AdministrateTopics, board);
    }

    /// <summary>
    /// The third flag of the same branch, and the one the first pass missed.
    /// Moving a topic to another board reaches the service through
    /// UpdateTopicRequest.Board; the author passes TopicIntention.Edit, does not
    /// enter the administrate branch, and the else branch used to name only the
    /// two boolean flags -- so BoardTitle was neither checked nor cleared, the
    /// repository was called with a null board id, and the author got 200 with
    /// the topic still in the old board.
    /// </summary>
    [Fact]
    public async Task RefuseTheBoardMoveTheCallerMayNotMake()
    {
        var topicId = Guid.NewGuid();
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var topic = new Topic { Id = topicId, Board = board, IsClosed = false, IsAttached = false };

        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));
        _intentionManager.IsAllowed(ForumIntention.AdministrateTopics, board).Returns(false);
        _intentionManager
            .When(m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = "Offtopic"
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused move writes nothing.
        await _repository.DidNotReceive().Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>());
    }

    /// <summary>
    /// The same round-trip rule as the flags: the client sends the whole topic
    /// back, so the board it is already in is nobody's attempt to move it.
    /// </summary>
    [Fact]
    public async Task NotRefuseTheBoardTheTopicIsAlreadyIn()
    {
        var topicId = Guid.NewGuid();
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var topic = new Topic { Id = topicId, Board = board, IsClosed = false, IsAttached = false };

        _repository.Get(topicId, Arg.Any<BoardAccessPolicy>(), Arg.Any<CancellationToken>()).Returns(topic);
        _repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, true));
        _intentionManager.IsAllowed(ForumIntention.AdministrateTopics, board).Returns(false);

        await _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = board.Title
        });

        _intentionManager.DidNotReceive().ThrowIfForbidden(ForumIntention.AdministrateTopics, board);
    }

    /// <summary>
    /// The pinned order is replaced whole, and the board of the address is what
    /// the write is bounded by.
    /// </summary>
    [Fact]
    public async Task WriteThePinnedOrderTheBodyNames()
    {
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _boardService.GetBoard("General", true).Returns(board);
        _repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var order = new[] { second, first };
        await _service.ReorderPinnedAsync("General", order);

        _intentionManager.Received(1).ThrowIfForbidden(ForumIntention.AdministrateTopics, board);
        // The board travels with the order: keyed by topic id alone, the write would
        // renumber the pinned topics of whatever board the body named.
        await _repository.Received(1).ReplaceAttachOrder(board.Id, order, default);
    }

    /// <summary>
    /// A body naming a subset is refused rather than half-applied: the topics it
    /// skips would keep the positions the same request has just handed to others,
    /// so two of them would share a place.
    /// </summary>
    [Fact]
    public async Task RefuseAPinnedOrderThatSkipsAPinnedTopic()
    {
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _boardService.GetBoard("General", true).Returns(board);
        _repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var act = () => _service.ReorderPinnedAsync("General", new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A refused order writes nothing.
        await _repository.DidNotReceive().ReplaceAttachOrder(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The same rule from the other side: a body of the right length that names
    /// one topic twice leaves another one with no position at all.
    /// </summary>
    [Fact]
    public async Task RefuseAPinnedOrderThatNamesOneTopicTwice()
    {
        var board = new Board { Id = Guid.NewGuid(), Title = "General" };
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        _boardService.GetBoard("General", true).Returns(board);
        _repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var act = () => _service.ReorderPinnedAsync("General", new[] { first, first });

        await act.Should().ThrowAsync<HttpBadRequestException>();
        await _repository.DidNotReceive().ReplaceAttachOrder(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }
}
