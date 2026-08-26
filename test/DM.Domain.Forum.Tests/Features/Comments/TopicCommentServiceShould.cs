using System;
using System.Collections.Generic;
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
using AwesomeAssertions;
using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Comments;

public class TopicCommentServiceShould : UnitTestBase
{
    private readonly ITopicService _topicService;
    private readonly IIntentionManager _intentionManager;
    private readonly IIdentityProvider _identityProvider;
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly ITopicCommentRepository _repository;
    private readonly IUnreadCountersRepository _countersRepository;
    private readonly IEventProducer _eventProducer;
    private readonly IUserBlacklistChecker _blacklistChecker;
    private readonly IBoardService _boardService;
    private readonly TopicCommentService _service;

    public TopicCommentServiceShould()
    {
        var createValidator = Mock<IValidator<CreateComment>>();
        createValidator
            .ValidateAsync(Arg.Any<ValidationContext<CreateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var updateValidator = Mock<IValidator<UpdateComment>>();
        updateValidator
            .ValidateAsync(Arg.Any<ValidationContext<UpdateComment>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _topicService = Mock<ITopicService>();
        _boardService = Mock<IBoardService>();

        _intentionManager = Mock<IIntentionManager>();

        _identityProvider = Mock<IIdentityProvider>();
        _identityProvider.Current.Returns(Identities.User(_currentUserId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        _repository = Mock<ITopicCommentRepository>();

        _countersRepository = Mock<IUnreadCountersRepository>();
        _countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>()).Returns(Task.CompletedTask);

        _eventProducer = Mock<IEventProducer>();
        _eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        _blacklistChecker = Mock<IUserBlacklistChecker>();
        _blacklistChecker.IsBlockedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        _service = new TopicCommentService(
            createValidator,
            updateValidator,
            _topicService,
            _boardService,
            _intentionManager,
            _identityProvider,
            dateTimeProvider,
            _repository,
            _countersRepository,
            _eventProducer,
            _blacklistChecker);
    }

    /// <summary>The store answers a creation with this comment.</summary>
    private void CreateReturns(Comment comment) =>
        _repository.Create(Arg.Any<CreateTopicCommentEntity>()).Returns(comment);

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        CreateReturns(new Comment());

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await _service.CreateAsync(createComment);

        _intentionManager.Received(1).ThrowIfForbidden(TopicIntention.CreateComment, topic);
    }

    [Fact]
    public async Task CreateCommentAndIncrementUnreadCounters()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 5 };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        var expectedComment = new Comment { Id = commentId };
        CreateReturns(expectedComment);

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        var result = await _service.CreateAsync(createComment);

        result.Should().Be(expectedComment);
        await _countersRepository.Received(1).IncrementAsync(topicId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task PublishNewCommentEvent()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        CreateReturns(new Comment { Id = commentId });

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await _service.CreateAsync(createComment);

        await _eventProducer.Received(1).SendAsync(EventType.NewTopicComment, commentId);
    }

    [Fact]
    public async Task ThrowWhenCommentingOnTopicByBlockedUser()
    {
        var topicId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var currentUserId = _identityProvider.Current.User.UserId;
        var topic = new Topic
        {
            Id = topicId,
            Author = new GeneralUser { UserId = authorId }
        };
        _topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        _blacklistChecker.IsBlockedAsync(authorId, currentUserId, Arg.Any<CancellationToken>()).Returns(true);

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        var act = async () => await _service.CreateAsync(createComment);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        exception.Which.Message.Should().Contain("Вы не можете комментировать этот топик");
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Original text" };
        _repository.Get(commentId).Returns(comment);
        _repository.Update(Arg.Any<UpdateTopicCommentEntity>()).Returns(comment);

        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated text" };
        await _service.UpdateAsync(updateComment);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
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
        _repository.GetForDelete(commentId).Returns(comment);
        DeleteTopicCommentEntity? deleted = null;
        _repository.Delete(Arg.Any<DeleteTopicCommentEntity>())
            .Returns(Task.CompletedTask)
            .AndDoes(ci =>
            {
                var entity = ci.ArgAt<DeleteTopicCommentEntity>(0);
                deleted = entity;
            });

        await _service.DeleteAsync(commentId);

        _intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
        // The author of the removal travels with it: Comment is ISoftDeletable and the
        // column stays empty unless the service hands the identity over. Forum comments and
        // blog comments live in one table, so a forum comment that does not carry it makes
        // every report over that column wrong rather than incomplete.
        deleted!.DeletedByUserId.Should().Be(_currentUserId);
        deleted.DeletedUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        _repository.GetForDelete(commentId).Returns((TopicCommentToDelete)null!);

        var act = async () => await _service.DeleteAsync(commentId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain($"Комментарий {commentId} не найден");
    }

    /// <summary>
    /// What the topic's comments counter has to answer: where this reader
    /// stopped, which is the first comment past his read marker.
    /// </summary>
    [Fact]
    public async Task LeadToTheFirstCommentPastTheReadMarker()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var lastRead = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var firstUnread = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 41 };

        _topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        _countersRepository
            .GetLastReadTimeAsync(
                _identityProvider.Current.User.UserId, topic.Id, UnreadEntryType.Message).Returns(lastRead);
        _repository
            .FindFirstUnread(topic.Id, new DateTimeOffset(lastRead), null).Returns(firstUnread);

        var result = await _service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(firstUnread);
        await _repository.DidNotReceive().GetLastComment(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>?>());
    }

    /// <summary>
    /// Nothing unread is the ordinary state one second after the topic was
    /// opened: opening it flushes the marker. Answering with the beginning of
    /// the topic there is what made the same link useless on the second click.
    /// </summary>
    [Fact]
    public async Task FallBackToTheLastCommentWhenNothingIsUnread()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var lastRead = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastComment = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 60 };

        _topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        _countersRepository
            .GetLastReadTimeAsync(
                _identityProvider.Current.User.UserId, topic.Id, UnreadEntryType.Message).Returns(lastRead);
        _repository
            .FindFirstUnread(topic.Id, Arg.Any<DateTimeOffset>(), null).Returns((FirstUnreadComment?)null);
        _repository
            .GetLastComment(topic.Id, null).Returns(lastComment);

        var result = await _service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(lastComment);
    }

    /// <summary>
    /// A reader who has never opened the topic has no marker at all, and every
    /// comment is unread to him: he starts at the first one. Folding that case
    /// into the fallback above would send him straight to the end.
    /// </summary>
    [Fact]
    public async Task StartAtTheBeginningForAReaderWithoutAMarker()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var firstComment = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 1 };

        _topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        _countersRepository
            .GetLastReadTimeAsync(Arg.Any<Guid>(), topic.Id, UnreadEntryType.Message).Returns((DateTime?)null);
        _repository
            .FindFirstUnread(topic.Id, DateTimeOffset.MinValue, null).Returns(firstComment);

        var result = await _service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(firstComment);
    }
    /// <summary>
    /// Marking the whole forum read asks for the boards and nothing else.
    /// </summary>
    /// <remarks>
    /// The list with counters is the expensive one: it fills a per-board unread
    /// number from two aggregate reads over the document store and three queries
    /// behind them. This loop reads identifiers, and the very call it makes with
    /// them is what sets every one of those numbers to zero — so the work was
    /// computed, carried across a service boundary and discarded a line later,
    /// every time somebody clicked the link.
    ///
    /// Invisible from the outside: the page answers, the badges clear, and the
    /// only difference is a handful of queries nobody counts.
    /// </remarks>
    [Fact]
    public async Task AskForTheBoardsWithoutTheCountersItIsAboutToZero()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        _boardService
            .GetAvailableBoards().Returns([new Board { Id = first }, new Board { Id = second }]);

        await _service.MarkAllAsReadAsync();

        await _countersRepository.Received(1).FlushAllAsync(_currentUserId, UnreadEntryType.Message, first);
        await _countersRepository.Received(1).FlushAllAsync(_currentUserId, UnreadEntryType.Message, second);
        // The counters that call fills are the ones this method zeroes, so every read
        // behind them is work computed and thrown away.
        await _boardService.DidNotReceive().GetBoardsList();
    }
}
