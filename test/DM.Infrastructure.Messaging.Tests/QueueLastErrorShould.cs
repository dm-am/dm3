using System;
using AwesomeAssertions;
using DM.Infrastructure.Messaging.Queues;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

/// <summary>
/// What a failed row keeps of the exception: what the operator reads before returning it.
/// </summary>
public class QueueLastErrorShould
{
    [Fact]
    public void KeepTheTypeAndTheMessage()
    {
        QueueWorker.LastError(new TimeoutException("the mail server is down"))
            .Should().Be("System.TimeoutException: the mail server is down");
    }

    /// <remarks>Mutations it must fail on: zero characters kept, which PostgreSQL text refuses, and no cut.</remarks>
    [Fact]
    public void CutTo500CharactersWithZeroCharactersRemoved()
    {
        var message = "\0" + new string('x', 600);

        var kept = QueueWorker.LastError(new InvalidOperationException(message));

        kept.Should().HaveLength(500).And.NotContain("\0").And.StartWith("System.InvalidOperationException: xxx");
    }

    [Fact]
    public void NeverCutASurrogatePairInHalf()
    {
        const string prefix = "System.Exception: ";
        var message = new string('x', 500 - prefix.Length - 1) + "\U0001F600" + "tail";

        var kept = QueueWorker.LastError(new Exception(message));

        kept.Should().HaveLength(499, "the pair that would straddle the cut is left out whole");
        char.IsHighSurrogate(kept[^1]).Should().BeFalse();
    }
}
