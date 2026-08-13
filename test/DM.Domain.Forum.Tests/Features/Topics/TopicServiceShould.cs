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
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class TopicServiceShould : UnitTestBase
{
    private readonly Mock<IBoardService> _boardService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<IAccessPolicyConverter> _accessPolicyConverter;
    private readonly Mock<ITopicRepository> _repository;
    private readonly Mock<IGuidFactory> _guidFactory;
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly ISetup<ITopicRepository, Task<Topic>> _createTopicSetup;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly TopicService _service;

    public TopicServiceShould()
    {
        var createValidator = Mock<IValidator<CreateTopic>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateTopic>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateTopic>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateTopic>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _boardService = Mock<IBoardService>();

        _accessPolicyConverter = Mock<IAccessPolicyConverter>();
        _accessPolicyConverter.Setup(c => c.Convert(It.IsAny<UserRole>()))
            .Returns(BoardAccessPolicy.Guest);

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<ForumIntention>(), It.IsAny<Board>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<TopicIntention>(), It.IsAny<Topic>()));
        _intentionManager.Setup(m => m.IsAllowed(It.IsAny<ForumIntention>(), It.IsAny<Board>()))
            .Returns(false);

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        _repository = Mock<ITopicRepository>();
        _createTopicSetup = _repository.Setup(r => r.Create(
            It.IsAny<CreateTopicEntity>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()));

        _guidFactory = Mock<IGuidFactory>();
        _guidFactory.Setup(f => f.Create()).Returns(Guid.NewGuid);

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.CreateMarkerAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);
        _unreadCountersRepository.Setup(r => r.SelectByEntitiesAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>(), It.IsAny<Guid[]>()))
            .ReturnsAsync((Guid userId, UnreadEntryType type, Guid[] ids) =>
                ids.ToDictionary(id => id, _ => 0));

        _userLookupService = Mock<IUserLookupService>();

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        // Pass-through cache: always delegates to the factory so the
        // service's cacheable fast path still calls the repository in
        // tests. We don't want to test the cache here, only the
        // underlying read logic.
        var cache = Mock<ICache>();
        cache
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<object>(),
                It.IsAny<Func<Task<Topic[]>>>(),
                It.IsAny<TimeSpan>()))
            .Returns<object, Func<Task<Topic[]>>, TimeSpan>((_, factory, _) => factory());

        _service = new TopicService(
            createValidator.Object,
            updateValidator.Object,
            _boardService.Object,
            _accessPolicyConverter.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            _repository.Object,
            _unreadCountersRepository.Object,
            _userLookupService.Object,
            _eventProducer.Object,
            _guidFactory.Object,
            cache.Object);
    }

    [Fact]
    public async Task AuthorizeCreateTopicAction()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);
        _createTopicSetup.ReturnsAsync(new Topic());

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.CreateAsync(createTopic);

        _intentionManager.Verify(m => m.ThrowIfForbidden(ForumIntention.CreateTopic, board), Times.Once);
    }

    [Fact]
    public async Task CreateTopicAndInitializeUnreadCounters()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        _guidFactory.Setup(f => f.Create()).Returns(topicId);

        var expectedTopic = new Topic { Id = topicId };
        _createTopicSetup.ReturnsAsync(expectedTopic);

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        var result = await _service.CreateAsync(createTopic);

        result.Should().Be(expectedTopic);
        _unreadCountersRepository.Verify(
            r => r.CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message),
            Times.Once);
        _unreadCountersRepository.Verify(
            r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()),
            Times.Never,
            "the row landed, so the reservation was committed");
    }

    [Fact]
    public async Task TakeTheUnreadMarkerBackWhenTheTopicRowDoesNotLand()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        _boardService.Setup(s => s.GetBoard("General", true))
            .ReturnsAsync(new Board { Id = boardId, Title = "General" });
        _guidFactory.Setup(f => f.Create()).Returns(topicId);
        _createTopicSetup.ThrowsAsync(new InvalidOperationException("storage refused"));

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.Awaiting(s => s.CreateAsync(createTopic))
            .Should().ThrowAsync<InvalidOperationException>(
                "the refusal of the store is what the caller has to see");

        // Written first and taken back, so the failure loses a topic nobody has
        // seen. The other order left a committed topic whose counters never exist.
        _unreadCountersRepository.Verify(
            r => r.CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message), Times.Once);
        _unreadCountersRepository.Verify(
            r => r.DeleteAsync(topicId, UnreadEntryType.Message), Times.Once);
    }

    [Fact]
    public async Task PublishNewTopicEvent()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);
        _createTopicSetup.ReturnsAsync(new Topic { Id = topicId });

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await _service.CreateAsync(createTopic);

        _eventProducer.Verify(p => p.SendAsync(EventType.NewTopic, topicId), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTopicNeverExisted()
    {
        var topicId = Guid.NewGuid();
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic)null!);
        _repository.Setup(r => r.Exists(topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var act = async () => await _service.GetAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain("Топик не найден");
    }

    [Fact]
    public async Task ThrowGoneWhenTopicWasRemoved()
    {
        var topicId = Guid.NewGuid();
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Topic)null!);
        _repository.Setup(r => r.Exists(topicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await _service.UpdateAsync(updateTopic);

        _intentionManager.Verify(m => m.ThrowIfForbidden(TopicIntention.Edit, topic), Times.Once);
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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await _service.UpdateAsync(updateTopic);

        _eventProducer.Verify(p => p.SendAsync(EventType.ChangedTopic, topicId), Times.Once);
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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, false));

        var result = await _service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Same Title" });

        result.Should().BeSameAs(topic, "the request is answered either way — it is not an error");
        _eventProducer.Verify(p => p.SendAsync(EventType.ChangedTopic, It.IsAny<Guid>()), Times.Never);
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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        UpdateTopicEntity? passed = null;
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .Callback((UpdateTopicEntity entity, Guid? _) => passed = entity)
            .ReturnsAsync(new TopicUpdateResult(topic, true));

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

        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), newBoardId))
            .ReturnsAsync(new TopicUpdateResult(topic, true));

        _intentionManager.Setup(m => m.IsAllowed(ForumIntention.AdministrateTopics, oldBoard))
            .Returns(true);
        _boardService.Setup(s => s.GetBoard("NewBoard", false)).ReturnsAsync(newBoard);

        var updateTopic = new UpdateTopic { TopicId = topicId, BoardTitle = "NewBoard" };
        await _service.UpdateAsync(updateTopic);

        _unreadCountersRepository.Verify(
            r => r.ChangeParentAsync(oldBoardId, UnreadEntryType.Message, newBoardId),
            Times.Once);
        _repository.Verify(r => r.Update(It.IsAny<UpdateTopicEntity>(), newBoardId), Times.Once);
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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Delete(topicId, _currentUserId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(topicId);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, topic.Board),
            Times.Once);
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
        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Delete(topicId, _currentUserId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(topicId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        _repository.Verify(r => r.Delete(topicId, _currentUserId), Times.Once);
        _unreadCountersRepository.Verify(
            r => r.DeleteAsync(topicId, UnreadEntryType.Message),
            Times.Once);
        _eventProducer.Verify(p => p.SendAsync(EventType.DeletedTopic, topicId), Times.Once);
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

        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));
        _intentionManager.Setup(m => m.IsAllowed(ForumIntention.AdministrateTopics, board))
            .Returns(false);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board))
            .Throws(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = isClosed,
            IsAttached = isAttached
        });

        await act.Should().ThrowAsync<HttpException>();
        _repository.Verify(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()), Times.Never,
            "a refused change writes nothing");
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

        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));
        _intentionManager.Setup(m => m.IsAllowed(ForumIntention.AdministrateTopics, board))
            .Returns(false);

        await _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = true,
            IsAttached = false
        });

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board), Times.Never);
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

        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));
        _intentionManager.Setup(m => m.IsAllowed(ForumIntention.AdministrateTopics, board))
            .Returns(false);
        _intentionManager
            .Setup(m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board))
            .Throws(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = "Offtopic"
        });

        await act.Should().ThrowAsync<HttpException>();
        _repository.Verify(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()), Times.Never,
            "a refused move writes nothing");
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

        _repository.Setup(r => r.Get(topicId, It.IsAny<BoardAccessPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(topic);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicEntity>(), It.IsAny<Guid?>()))
            .ReturnsAsync(new TopicUpdateResult(topic, true));
        _intentionManager.Setup(m => m.IsAllowed(ForumIntention.AdministrateTopics, board))
            .Returns(false);

        await _service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = board.Title
        });

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board), Times.Never);
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
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);
        _repository.Setup(r => r.GetAttachedTopicIds(board.Id, default))
            .ReturnsAsync(new[] { first, second });

        var order = new[] { second, first };
        await _service.ReorderPinnedAsync("General", order);

        _intentionManager.Verify(
            m => m.ThrowIfForbidden(ForumIntention.AdministrateTopics, board), Times.Once);
        _repository.Verify(r => r.ReplaceAttachOrder(board.Id, order, default), Times.Once,
            "the board travels with the order: keyed by topic id alone, the write would " +
            "renumber the pinned topics of whatever board the body named");
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
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);
        _repository.Setup(r => r.GetAttachedTopicIds(board.Id, default))
            .ReturnsAsync(new[] { first, second });

        var act = () => _service.ReorderPinnedAsync("General", new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _repository.Verify(
            r => r.ReplaceAttachOrder(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), default),
            Times.Never, "a refused order writes nothing");
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
        _boardService.Setup(s => s.GetBoard("General", true)).ReturnsAsync(board);
        _repository.Setup(r => r.GetAttachedTopicIds(board.Id, default))
            .ReturnsAsync(new[] { first, second });

        var act = () => _service.ReorderPinnedAsync("General", new[] { first, first });

        await act.Should().ThrowAsync<HttpBadRequestException>();
        _repository.Verify(
            r => r.ReplaceAttachOrder(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), default),
            Times.Never);
    }
}
