using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Comments;
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
    private readonly ITopicService topicService;
    private readonly IIntentionManager intentionManager;
    private readonly IIdentityProvider identityProvider;
    private readonly Guid currentUserId = Guid.NewGuid();
    private readonly ITopicCommentRepository repository;
    private readonly IUnreadCountersRepository countersRepository;
    private readonly IEventProducer eventProducer;
    private readonly IBoardService boardService;
    private readonly TopicCommentService service;

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

        topicService = Mock<ITopicService>();
        boardService = Mock<IBoardService>();

        intentionManager = Mock<IIntentionManager>();

        identityProvider = Mock<IIdentityProvider>();
        identityProvider.Current.Returns(Identities.User(currentUserId, UserRole.RegularUser));

        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        repository = Mock<ITopicCommentRepository>();

        countersRepository = Mock<IUnreadCountersRepository>();
        countersRepository.IncrementAsync(Arg.Any<Guid>(), Arg.Any<UnreadEntryType>()).Returns(Task.CompletedTask);

        eventProducer = Mock<IEventProducer>();
        eventProducer.SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>()).Returns(Task.CompletedTask);

        service = new TopicCommentService(
            createValidator,
            updateValidator,
            topicService,
            boardService,
            intentionManager,
            identityProvider,
            dateTimeProvider,
            repository,
            countersRepository,
            eventProducer);
    }

    private void CreateReturns(Comment comment) =>
        repository.Create(Arg.Any<CreateTopicCommentEntity>()).Returns(comment);

    [Fact]
    public async Task AuthorizeCreateCommentAction()
    {
        var topicId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        CreateReturns(new Comment());

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await service.CreateAsync(createComment);

        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.CreateComment, topic);
    }

    [Fact]
    public async Task CreateCommentAndIncrementUnreadCounters()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 5 };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);

        var expectedComment = new Comment { Id = commentId };
        CreateReturns(expectedComment);

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        var result = await service.CreateAsync(createComment);

        result.Should().Be(expectedComment);
        await countersRepository.Received(1).IncrementAsync(topicId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task StoreTheTextAsTyped()
    {
        var topicId = Guid.NewGuid();
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(new Topic { Id = topicId });
        CreateReturns(new Comment());

        await service.CreateAsync(new CreateComment { EntityId = topicId, Text = "  \n Test comment \n " });

        await repository.Received(1).Create(Arg.Is<CreateTopicCommentEntity>(e => e.Text == "  \n Test comment \n "));
    }

    [Fact]
    public async Task PublishNewCommentEvent()
    {
        var topicId = Guid.NewGuid();
        var commentId = Guid.NewGuid();
        var topic = new Topic { Id = topicId, TotalCommentsCount = 0 };
        topicService.GetAsync(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        CreateReturns(new Comment { Id = commentId });

        var createComment = new CreateComment { EntityId = topicId, Text = "Test comment" };
        await service.CreateAsync(createComment);

        await eventProducer.Received(1).SendAsync(EventType.NewTopicComment, commentId);
    }

    [Fact]
    public async Task AuthorizeUpdateCommentAction()
    {
        var commentId = Guid.NewGuid();
        var comment = new Comment { Id = commentId, Text = "Original text" };
        repository.Get(commentId).Returns(comment);
        repository.Update(Arg.Any<UpdateTopicCommentEntity>()).Returns(comment);

        var updateComment = new UpdateComment { CommentId = commentId, Text = "Updated text" };
        await service.UpdateAsync(updateComment);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Edit, comment);
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
            CreatedUtc = DateTimeOffset.UtcNow
        };
        repository.GetForDelete(commentId).Returns(comment);
        DeleteTopicCommentEntity? deleted = null;
        repository.Delete(Arg.Do<DeleteTopicCommentEntity>(entity => deleted = entity))
            .Returns(true);

        await service.DeleteAsync(commentId);

        intentionManager.Received(1).ThrowIfForbidden(CommentIntention.Delete, Arg.Any<Comment>());
        // The author of the removal travels with it: Comment is ISoftDeletable and the
        // column stays empty unless the service hands the identity over. Forum comments and
        // blog comments live in one table, so a forum comment that does not carry it makes
        // every report over that column wrong rather than incomplete.
        deleted!.DeletedByUserId.Should().Be(currentUserId);
        deleted.DeletedUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ThrowWhenDeletingNonExistentComment()
    {
        var commentId = Guid.NewGuid();
        repository.GetForDelete(commentId).Returns((TopicCommentToDelete)null!);

        var act = async () => await service.DeleteAsync(commentId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain($"Комментарий {commentId} не найден");
    }

    [Fact]
    public async Task LeadToTheFirstCommentPastTheReadMarker()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var lastRead = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var firstUnread = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 41 };

        topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        countersRepository
            .GetLastReadTimeAsync(
                identityProvider.Current.User.UserId, topic.Id, UnreadEntryType.Message).Returns(lastRead);
        repository
            .FindFirstUnread(topic.Id, new DateTimeOffset(lastRead), null).Returns(firstUnread);

        var result = await service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(firstUnread);
        await repository.DidNotReceive().GetLastComment(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>?>());
    }

    // Nothing unread is the ordinary state one second after the topic was opened: opening it
    // flushes the marker.
    [Fact]
    public async Task FallBackToTheLastCommentWhenNothingIsUnread()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var lastRead = new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastComment = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 60 };

        topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        countersRepository
            .GetLastReadTimeAsync(
                identityProvider.Current.User.UserId, topic.Id, UnreadEntryType.Message).Returns(lastRead);
        repository
            .FindFirstUnread(topic.Id, Arg.Any<DateTimeOffset>(), null).Returns((FirstUnreadComment?)null);
        repository
            .GetLastComment(topic.Id, null).Returns(lastComment);

        var result = await service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(lastComment);
    }

    // A reader who has never opened the topic has no marker at all, and every comment is unread to
    // him: he starts at the first one.
    [Fact]
    public async Task StartAtTheBeginningForAReaderWithoutAMarker()
    {
        var topic = new Topic { Id = Guid.NewGuid() };
        var firstComment = new FirstUnreadComment { CommentId = Guid.NewGuid(), CommentNumber = 1 };

        topicService
            .GetByBoardAndNumberAsync("news", 3, Arg.Any<CancellationToken>()).Returns(topic);
        countersRepository
            .GetLastReadTimeAsync(Arg.Any<Guid>(), topic.Id, UnreadEntryType.Message).Returns((DateTime?)null);
        repository
            .FindFirstUnread(topic.Id, DateTimeOffset.MinValue, null).Returns(firstComment);

        var result = await service.GetFirstUnreadAsync("news", 3);

        result.Should().BeSameAs(firstComment);
    }

    // The list with counters is the expensive one, and this call zeroes every counter it would
    // fill.
    [Fact]
    public async Task AskForTheBoardsWithoutTheCountersItIsAboutToZero()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        boardService
            .GetBoardsWithoutCounters().Returns([new Board { Id = first }, new Board { Id = second }]);

        await service.MarkAllAsReadAsync();

        await countersRepository.Received(1).FlushAllAsync(currentUserId, UnreadEntryType.Message, first);
        await countersRepository.Received(1).FlushAllAsync(currentUserId, UnreadEntryType.Message, second);
        // The counters that call fills are the ones this method zeroes, so every read
        // behind them is work computed and thrown away.
        await boardService.DidNotReceive().GetBoardsList();
    }

    // Two removals of one comment would take the counter down twice, and it has no way back.
    [Fact]
    public async Task LeaveTheCountersToTheRemovalThatTookTheComment()
    {
        var commentId = Guid.NewGuid();
        repository.GetForDelete(commentId).Returns(new TopicCommentToDelete
        {
            Id = commentId,
            EntityId = Guid.NewGuid(),
            CreatedUtc = DateTimeOffset.UtcNow,
        });
        repository.Delete(Arg.Any<DeleteTopicCommentEntity>()).Returns(false);

        await service.DeleteAsync(commentId);

        await countersRepository.DidNotReceive().DecrementAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>(), Arg.Any<DateTimeOffset>());
        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }
}
