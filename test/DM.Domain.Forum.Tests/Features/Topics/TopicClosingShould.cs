using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Topics;
using DM.Domain.Forum.Tests.Dsl;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

public class TopicClosingShould : TopicServiceTestBase
{
    /// <remarks>
    /// A closed topic is shut to its author's edits, and the edit form is how a
    /// topic is closed and reopened: judged against the closed topic, the author's
    /// reopening would be refused by the very state it undoes. Whoever may reopen
    /// may edit the open topic, which TopicIntentionResolverShould holds.
    /// </remarks>
    [Fact]
    public async Task ReopenAClosedTopicForWhoeverMayCloseIt()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId, currentUserId, isClosed: true, closedByAuthor: true);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);
        intentionManager
            .When(m => m.ThrowIfForbidden(TopicIntention.Edit, topic))
            .Throw(new HttpException(HttpStatusCode.Forbidden, "закрыт"));

        await service.UpdateAsync(new UpdateTopic
        {
            TopicId = topicId,
            Title = "Reopened",
            IsClosed = false
        });

        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Close, topic);
        await repository.Received(1).Update(
            Arg.Is<UpdateTopicEntity>(e => e.IsClosed == false && e.ClosedByAuthor == false && e.Title == "Reopened"),
            Arg.Any<Guid?>());
    }

    // Closing asks both rules: the right to close, and the right to edit the topic as it stands,
    // since the same request carries the form's text.
    [Fact]
    public async Task AskBothTheCloseAndTheEditRuleOfAClosingRequest()
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId, currentUserId);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(false);

        await service.UpdateAsync(new UpdateTopic { TopicId = topicId, IsClosed = true });

        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Close, topic);
        intentionManager.Received(1).ThrowIfForbidden(TopicIntention.Edit, topic);
        // The author is not an administrator of the board, and closing no longer
        // needs one.
        intentionManager.DidNotReceive().ThrowIfForbidden(BoardIntention.AdministrateTopics, topic.Board);
        await repository.Received(1).Update(
            Arg.Is<UpdateTopicEntity>(e => e.IsClosed == true && e.ClosedByAuthor == true), Arg.Any<Guid?>());
    }

    // Who closed the topic is written with every change of its closed state and only then: it is
    // what keeps a moderator's closing from being undone by the author (TopicIntention.Close), so a
    // close by anybody but the author records false, and an edit that leaves the state alone leaves
    // the record alone.
    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, null, null)]
    [InlineData(true, true, null)]
    public async Task RecordWhetherTheAuthorClosedTheTopic(bool wasClosed, bool? isClosed, bool? expected)
    {
        var topicId = Guid.NewGuid();
        var topic = SetupTopic(topicId, isClosed: wasClosed);
        intentionManager.IsAllowed(BoardIntention.AdministrateTopics, topic.Board).Returns(true);

        await service.UpdateAsync(new UpdateTopic { TopicId = topicId, Title = "Edited", IsClosed = isClosed });

        await repository.Received(1).Update(
            Arg.Is<UpdateTopicEntity>(e => e.ClosedByAuthor == expected), Arg.Any<Guid?>());
    }
}
