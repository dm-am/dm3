using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Forum.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class TopicServiceShould : TopicServiceTestBase
{
    /// <summary>The store answers a creation with this topic.</summary>
    private void CreateReturns(Topic topic) =>
        repository.Create(
            Arg.Any<CreateTopicEntity>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>()).Returns(topic);

    /// <summary>The store refuses a creation with this failure.</summary>
    private void CreateThrows(Exception failure) =>
        repository.Create(
            Arg.Any<CreateTopicEntity>(),
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>()).ThrowsAsync(failure);

    [Fact]
    public async Task AuthorizeCreateTopicAction()
    {
        var boardId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        boardService.GetBoard("General").Returns(board);
        CreateReturns(new Topic());

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await service.CreateAsync(createTopic);

        intentionManager.Received(1).ThrowIfForbidden(BoardIntention.CreateTopic, board);
    }

    [Fact]
    public async Task CreateTopicAndInitializeUnreadCounters()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        boardService.GetBoard("General").Returns(board);

        // The identifier is minted here and not learnt from the row: the marker is
        // written before the row exists to return one.
        guidFactory.Create().Returns(topicId);

        var expectedTopic = new Topic { Id = topicId };
        CreateReturns(expectedTopic);

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        var result = await service.CreateAsync(createTopic);

        result.Should().Be(expectedTopic);
        await unreadCountersRepository.Received(1).CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message);
        // The row landed, so the reservation was committed.
        await unreadCountersRepository.DidNotReceive().DeleteAsync(
            Arg.Any<Guid>(), Arg.Any<UnreadEntryType>());
    }

    [Fact]
    public async Task TakeTheUnreadMarkerBackWhenTheTopicRowDoesNotLand()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        boardService.GetBoard("General").Returns(new Board { Id = boardId, Title = "General" });
        guidFactory.Create().Returns(topicId);
        CreateThrows(new InvalidOperationException("storage refused"));

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await service.Awaiting(s => s.CreateAsync(createTopic))
            .Should().ThrowAsync<InvalidOperationException>(
                "the refusal of the store is what the caller has to see");

        // Written first and taken back, so the failure loses a topic nobody has
        // seen. The other order left a committed topic whose counters never exist.
        await unreadCountersRepository.Received(1).CreateMarkerAsync(topicId, boardId, UnreadEntryType.Message);
        await unreadCountersRepository.Received(1).DeleteAsync(topicId, UnreadEntryType.Message);
    }

    [Fact]
    public async Task PublishNewTopicEvent()
    {
        var boardId = Guid.NewGuid();
        var topicId = Guid.NewGuid();
        var board = new Board { Id = boardId, Title = "General" };
        boardService.GetBoard("General").Returns(board);
        CreateReturns(new Topic { Id = topicId });

        var createTopic = new CreateTopic { BoardTitle = "General", Title = "Test Topic", Text = "Test" };
        await service.CreateAsync(createTopic);

        await eventProducer.Received(1).SendAsync(EventType.NewTopic, topicId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenTopicNeverExisted()
    {
        var topicId = Guid.NewGuid();
        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns((Topic)null!);
        repository.Exists(topicId, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await service.GetAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.NotFound);
        exception.Which.Message.Should().Contain("Топик не найден");
    }

    [Fact]
    public async Task ThrowGoneWhenTopicWasRemoved()
    {
        var topicId = Guid.NewGuid();
        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns((Topic)null!);
        repository.Exists(topicId, Arg.Any<CancellationToken>()).Returns(true);

        var act = async () => await service.GetAsync(topicId);

        var exception = await act.Should().ThrowAsync<HttpException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Gone);
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
        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        repository.Delete(topicId, currentUserId).Returns(Task.CompletedTask);

        await service.DeleteAsync(topicId);

        intentionManager.Received(1).ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board);
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
        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        repository.Delete(topicId, currentUserId).Returns(Task.CompletedTask);

        await service.DeleteAsync(topicId);

        // The author of the removal travels with it: ISoftDeletable promises who deleted the
        // row, and the column stays empty unless the service hands the identity over.
        await repository.Received(1).Delete(topicId, currentUserId);
        await unreadCountersRepository.Received(1).DeleteAsync(topicId, UnreadEntryType.Message);
        await eventProducer.Received(1).SendAsync(EventType.DeletedTopic, topicId);
    }
}
