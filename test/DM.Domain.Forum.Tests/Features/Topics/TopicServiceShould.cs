using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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
using DM.Domain.Forum.Tests.Dsl;
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
    private readonly Mock<IUnreadCountersRepository> _unreadCountersRepository;
    private readonly Mock<IUserLookupService> _userLookupService;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly ISetup<ITopicRepository, Task<Topic>> _createTopicSetup;
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

        var userId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identity.User(userId, UserRole.RegularUser));

        _repository = Mock<ITopicRepository>();
        _createTopicSetup = _repository.Setup(r => r.Create(
            It.IsAny<CreateTopicEntity>(),
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()));

        _unreadCountersRepository = Mock<IUnreadCountersRepository>();
        _unreadCountersRepository.Setup(r => r.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
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

        var expectedTopic = new Topic { Id = topicId };
        _createTopicSetup.ReturnsAsync(expectedTopic);

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        var result = await _service.CreateAsync(createTopic);

        result.Should().Be(expectedTopic);
        _unreadCountersRepository.Verify(
            r => r.CreateAsync(topicId, boardId, UnreadEntryType.Message),
            Times.Once);
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
        exception.Which.Message.Should().Contain("Topic not found");
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
            .ReturnsAsync(topic);

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
            .ReturnsAsync(topic);

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await _service.UpdateAsync(updateTopic);

        _eventProducer.Verify(p => p.SendAsync(EventType.ChangedTopic, topicId), Times.Once);
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
            .ReturnsAsync(topic);

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
        _repository.Setup(r => r.Delete(topicId)).Returns(Task.CompletedTask);

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
        _repository.Setup(r => r.Delete(topicId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(topicId);

        _repository.Verify(r => r.Delete(topicId), Times.Once);
        _unreadCountersRepository.Verify(
            r => r.DeleteAsync(topicId, UnreadEntryType.Message),
            Times.Once);
        _eventProducer.Verify(p => p.SendAsync(EventType.DeletedTopic, topicId), Times.Once);
    }
}
