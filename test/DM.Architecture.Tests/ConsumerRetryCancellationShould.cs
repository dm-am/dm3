using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A subscription being retried still notices the host stopping.
/// </summary>
/// <remarks>
/// The synchronous overload spends its waits in Thread.Sleep with no token, so a stopping host
/// waits every attempt out. Asserted on the text: reaching it at runtime needs a refusing broker.
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
        RepositoryLayout.Root, relative.Replace('/', Path.DirectorySeparatorChar));
}
