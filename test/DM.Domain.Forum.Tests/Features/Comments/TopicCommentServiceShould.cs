using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Blacklists;
using DM.Domain.Core.Comments;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.UnreadCounters;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Forum.Features.Topics;
using DM.Testing.Dsl;
using DM.Testing;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Moq.Language.Flow;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Comments;

public class TopicCommentServiceShould : UnitTestBase
{
    private readonly Mock<ITopicService> _topicService;
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly Mock<ITopicCommentRepository> _repository;
    private readonly Mock<IUnreadCountersRepository> _countersRepository;
    private readonly Mock<IEventProducer> _eventProducer;
    private readonly Mock<IUserBlacklistChecker> _blacklistChecker;
    private readonly ISetup<ITopicCommentRepository, Task<Comment>> _createCommentSetup;
    private readonly TopicCommentService _service;

    public TopicCommentServiceShould()
    {
        var createValidator = Mock<IValidator<CreateComment>>();
        createValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<CreateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateComment>>();
        updateValidator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<UpdateComment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _topicService = Mock<ITopicService>();
        var boardService = Mock<IBoardService>();

        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<TopicIntention>(), It.IsAny<Topic>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<CommentIntention>(), It.IsAny<Comment>()));

        var userId = Guid.NewGuid();
        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(userId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(d => d.Now).Returns(DateTimeOffset.UtcNow);

        _repository = Mock<ITopicCommentRepository>();
        _createCommentSetup = _repository.Setup(r => r.Create(It.IsAny<CreateTopicCommentEntity>()));

        _countersRepository = Mock<IUnreadCountersRepository>();
        _countersRepository.Setup(r => r.IncrementAsync(It.IsAny<Guid>(), It.IsAny<UnreadEntryType>()))
            .Returns(Task.CompletedTask);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.Setup(p => p.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _blacklistChecker = Mock<IUserBlacklistChecker>();
        _blacklistChecker.Setup(c => c.IsBlockedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _service = new TopicCommentService(
            createValidator.Object,
            updateValidator.Object,
            _topicService.Object,
            boardService.Object,
            _intentionManager.Object,
            _identityProvider.Object,
            dateTimeProvider.Object,
            _repository.Object,
            _countersRepository.Object,
            _eventProducer.Object,
            _blacklistChecker.Object);
    }

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        _createCommentSetup.ReturnsAsync(new Comment());

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await _service.CreateAsync(createComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(TopicIntention.CreateComment, topic), Times.Once);
    }

    [Fact]
    public async Task CreateCommentAndIncrementUnreadCounters()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 5 };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);

        var expectedComment = new Comment { Id = commentId };
        _createCommentSetup.ReturnsAsync(expectedComment);

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        var result = await _service.CreateAsync(createComment);

        result.Should().Be(expectedComment);
        _countersRepository.Verify(r => r.IncrementAsync(topicId, UnreadEntryType.Message), Times.Once);
    }

    [Fact]
    public async Task PublishNewCommentEvent()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        _createCommentSetup.ReturnsAsync(new Comment { Id = commentId });

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await _service.CreateAsync(createComment);

        _eventProducer.Verify(p => p.SendAsync(EventType.NewTopicComment, commentId), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenCommentingOnTopicByBlockedUser()
    {
        var topicId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Object.Current.User.UserId;
        var topic = new Topic
        {
            Id = topicId,
            Author = new GeneralUser { UserId = authorId }
        };
        _topicService.Setup(s => s.GetAsync(topicId, It.IsAny<CancellationToken>())).ReturnsAsync(topic);
        _blacklistChecker.Setup(c => c.IsBlockedAsync(authorId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        var act = async () => await _service.CreateAsync(createComment);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("Вы не можете комментировать эту тему");
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Original text" };
        _repository.Setup(r => r.Get(commentId)).ReturnsAsync(comment);
        _repository.Setup(r => r.Update(It.IsAny<UpdateTopicCommentEntity>())).ReturnsAsync(comment);

        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated text" };
        await _service.UpdateAsync(updateComment);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Edit, comment), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteCommentAction()
    {
        var commentId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var comment = new TopicCommentToDelete
        {
            Id = commentId,
            EntityId = topicId,
            IsLastComment = false,
            CreatedUtc = DateTimeOffset.UtcNow
        };
        _repository.Setup(r => r.GetForDelete(commentId)).ReturnsAsync(comment);
        _repository.Setup(r => r.Delete(It.IsAny<DeleteTopicCommentEntity>())).Returns(Task.CompletedTask);

        await _service.DeleteAsync(commentId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(CommentIntention.Delete, It.IsAny<Comment>()), Times.Once);
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        _repository.Setup(r => r.GetForDelete(commentId)).ReturnsAsync((TopicCommentToDelete)null!);

        var act = async () => await _service.DeleteAsync(commentId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain($"Комментарий {commentId} не найден");
    }
}
