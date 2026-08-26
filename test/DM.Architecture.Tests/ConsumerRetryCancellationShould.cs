using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A subscription being retried still notices the host stopping.
/// </summary>
/// <remarks>
/// Every consumer in the tree opens its subscription behind the same policy: five
/// attempts with the wait doubling from one second, which is 62 seconds of waiting
/// end to end. The synchronous overload spends them in Thread.Sleep on a thread
/// pool thread and is handed no token, so a host asked to stop inside a broker
/// outage waits every remaining attempt out with nothing able to interrupt it, and
/// the cancellation is observed by nobody. Both workers were moved to the
/// asynchronous overload and the API consumer was left behind - beside a comment,
/// in the worker next door, describing the very shape it kept.
///
/// A rule rather than one test, because the shape repeats: a fourth consumer is
/// written by copying one of these three.
///
/// Asserted on the text with the comments removed, because which overload is
/// called and what is passed to it is one line of composition, and reaching it at
/// runtime needs a broker that refuses connections on cue.
/// </remarks>
public class ConsumerRetryCancellationShould
{
    /// <summary>Every background service that subscribes to a queue.</summary>
    private static readonly string[] Consumers =
    [
        "src/DM.Web.API/Realtime/RealtimeNotificationConsumer.cs",
        "src/DM.Workers.Mail/MailConsumer.cs",
        "src/DM.Workers.NotificationDispatcher/NotificationDispatcherConsumer.cs",
    ];

    /// <summary>A rule that reads nothing passes.</summary>
    [Fact]
    public void FindEveryConsumerItNames() =>
        Consumers.Where(name => !File.Exists(Absolute(name)))
            .Should().BeEmpty("a path that moved leaves this rule reading nothing");

    [Fact]
    public void RetryTheSubscriptionAsynchronously() =>
        Consumers
            .Where(name => SourceText.ReadCode(Absolute(name))
                .Contains(".WaitAndRetry(", StringComparison.Ordinal))
            .Should().BeEmpty(
                "the synchronous overload waits in Thread.Sleep and takes no token, so the " +
                "waits of a broker outage are a pool thread held through a shutdown that " +
                "cannot interrupt them");

    [Fact]
    public void ExecuteTheSubscriptionThroughTheRetryPolicy() =>
        Consumers
            .Where(name => !SourceText.ReadCode(Absolute(name))
                .Contains("_consumeRetryPolicy.ExecuteAsync(", StringComparison.Ordinal))
            .Should().BeEmpty(
                "the policy is what holds the attempts, and a subscription outside it is " +
                "one attempt against a broker that may simply not be up yet");

    [Fact]
    public void HandTheRetryTheTokenTheHostStopsWith() =>
        Consumers
            .Where(name => !SourceText.ReadCode(Absolute(name))
                .Contains("}, stoppingToken);", StringComparison.Ordinal))
            .Should().BeEmpty(
                "a policy executed without the token of the host cancels on nothing, and " +
                "the stop waits out every attempt that is left");

    private static string Absolute(string relative) => Path.Combine(
        RepositoryRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
