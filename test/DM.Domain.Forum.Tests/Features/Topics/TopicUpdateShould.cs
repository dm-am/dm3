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
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class TopicUpdateShould : TopicServiceTestBase
{
    [Fact]
    public async Task AuthorizeUpdateTopicAction()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId);

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await service.UpdateAsync(updateTopic);

        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Edit, topic);
    }

    [Fact]
    public async Task PublishChangedTopicEvent()
    {
        var topicId = Guid.NewGuid();
        SetupTopic(topicId);

        var updateTopic = new UpdateTopic { TopicId = topicId, Title = "Updated Title" };
        await service.UpdateAsync(updateTopic);

        await eventProducer.Received(1).SendAsync(EventType.ChangedTopic, topicId);
    }

    // The notification reads its actor from the edit history, which a save that changed nothing
    // does not write.
    [Fact]
    public async Task SendNoChangedTopicEventWhenTheUpdateChangedNothing()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId);
        repository.Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>()).Returns(new TopicUpdateResult(topic, false));

        var result = await service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Same Title" });

        result.Should().BeSameAs(topic, "the request is answered either way — it is not an error");
        await eventProducer.DidNotReceive().SendAsync(EventType.ChangedTopic, Arg.Any<Guid>());
    }

    // The topic row keeps no editor, so this field is the only record of who changed a topic.
    [Fact]
    public async Task RecordTheCurrentUserAsTheEditorOfTheTopic()
    {
        var topicId = Guid.NewGuid();
        SetupTopic(topicId);

        await service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Updated Title" });

        await repository.Received(1).Update(
            Arg.Is<UpdateTopicEntity>(e => e.EditorUserId == currentUserId), Arg.Any<Guid?>());
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

        repository.Get(topicId, Arg.Any<CancellationToken>()).Returns(topic);
        repository.Update(Arg.Any<UpdateTopicEntity>(), newBoardId).Returns(new TopicUpdateResult(topic, true));

        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, oldBoard).Returns(true);
        boardService.GetBoard("NewBoard").Returns(newBoard);

        var updateTopic = new UpdateTopic { TopicId = topicId, BoardTitle = "NewBoard" };
        await service.UpdateAsync(updateTopic);

        // The topic carries its own markers over, not the board it leaves: by the
        // board, every other topic of it would move too.
        await unreadCountersRepository.Received(1).ChangeParentAsync(topicId, UnreadEntryType.Message, newBoardId);
        await repository.Received(1).Update(Arg.Any<UpdateTopicEntity>(), newBoardId);
    }

    // Closing or pinning a topic the caller may not close or pin is refused, not dropped: a 200
    // with an open topic is what a successful close produces.
    [Theory]
    [InlineData(true, null)]
    [InlineData(null, true)]
    public async Task RefuseTheAdminFlagsTheCallerMayNotSet(bool? isClosed, bool? isAttached)
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);
        intentionManager
            .When(m => m.ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));
        // Closing has a rule of its own now, open to the author as well
        // (TopicIntention.Close); this caller is refused it.
        intentionManager
            .When(m => m.ThrowIfForbidden(TopicIntention.Close, topic))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = isClosed,
            IsAttached = isAttached
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused change writes nothing.
        await repository.DidNotReceive().Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>());
    }

    // The client round-trips the whole topic, so the flags it already carries are nobody's attempt
    // at anything and must not be refused.
    [Fact]
    public async Task NotRefuseTheAdminFlagsTheTopicAlreadyCarries()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId, isClosed: true);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);

        await service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            IsClosed = true,
            IsAttached = false
        });

        intentionManager.DidNotReceive().ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board);
    }

    [Fact]
    public async Task RefuseTheBoardMoveTheCallerMayNotMake()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);
        intentionManager
            .When(m => m.ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "нельзя"));

        var act = () => service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = "Offtopic"
        });

        await act.Should().ThrowAsync<HttpException>();
        // A refused move writes nothing.
        await repository.DidNotReceive().Update(Arg.Any<UpdateTopicEntity>(), Arg.Any<Guid?>());
    }

    // The same round-trip rule as the flags: the client sends the whole topic back, so the board it
    // is already in is nobody's attempt to move it.
    [Fact]
    public async Task NotRefuseTheBoardTheTopicIsAlreadyIn()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);

        await service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            BoardTitle = topic.Board.Title
        });

        intentionManager.DidNotReceive().ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board);
    }
}
