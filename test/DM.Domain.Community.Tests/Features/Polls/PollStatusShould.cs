using System;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Domain.Community.Tests.Features.Polls;

/// <summary>
/// The voting window, pinned at both of its edges.
/// </summary>
/// <remarks>
/// One rule answers three questions - may this user vote, may this user take a
/// vote back, and what the response calls the poll - and it used to be written
/// out separately at each of them. The window is half-open, so a poll is open at
/// the tick its start arrives and closed at the tick its end does; both edges are
/// asserted here because that is where two copies drift apart first.
/// </remarks>
public class PollStatusShould
{
    private static readonly DateTimeOffset Start = new(2024, 1, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = new(2024, 1, 20, 12, 0, 0, TimeSpan.Zero);

    private static Poll Window => new() { StartsUtc = Start, EndsUtc = End };

    [Fact]
    public void CallThePollPendingBeforeItsStart() =>
        Window.StatusAt(Start.AddTicks(-1)).Should().Be(PollStatus.Pending);

    [Fact]
    public void OpenThePollAtItsStart() =>
        Window.StatusAt(Start).Should().Be(PollStatus.Active);

    [Fact]
    public void KeepThePollOpenUntilTheLastTickBeforeItsEnd() =>
        Window.StatusAt(End.AddTicks(-1)).Should().Be(PollStatus.Active);

    [Fact]
    public void CloseThePollAtItsEnd() =>
        Window.StatusAt(End).Should().Be(PollStatus.Closed);

    [Fact]
    public void KeepThePollClosedAfterItsEnd() =>
        Window.StatusAt(End.AddDays(1)).Should().Be(PollStatus.Closed);
}
