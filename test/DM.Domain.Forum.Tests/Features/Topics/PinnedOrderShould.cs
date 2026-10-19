using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Exceptions;
using DM.Domain.Forum.Authorization;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Forum.Tests.Features.Topics;

/// <summary>
/// The order of the pinned topics of a board, replaced whole.
/// </summary>
public class PinnedOrderShould : TopicServiceTestBase
{
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
        boardService.GetBoard("General").Returns(board);
        repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var order = new[] { second, first };
        await service.ReorderPinnedAsync("General", order);

        intentionManager.Received(1).ThrowIfForbidden(BoardIntention.AdministrateTopics, board);
        // The board travels with the order: keyed by topic id alone, the write would
        // renumber the pinned topics of whatever board the body named.
        await repository.Received(1).ReplaceAttachOrder(board.Id, order, default);
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
        boardService.GetBoard("General").Returns(board);
        repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var act = () => service.ReorderPinnedAsync("General", new[] { second });

        var refusal = await act.Should().ThrowAsync<HttpBadRequestException>();
        refusal.Which.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // A refused order writes nothing.
        await repository.DidNotReceive().ReplaceAttachOrder(
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
        boardService.GetBoard("General").Returns(board);
        repository.GetAttachedTopicIds(board.Id, default).Returns(new[] { first, second });

        var act = () => service.ReorderPinnedAsync("General", new[] { first, first });

        await act.Should().ThrowAsync<HttpBadRequestException>();
        await repository.DidNotReceive().ReplaceAttachOrder(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>());
    }
}
