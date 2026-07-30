using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A message no consumer could process has to stop somewhere a human can reach.
/// </summary>
/// <remarks>
/// When the retry middleware runs out of attempts the exception escapes the
/// pipeline and the message is rejected without requeue. Whether that means kept
/// or gone is decided by one line of the consumer parameters: a queue with no
/// dead-letter exchange has its poison messages dropped by the broker. The mail
/// worker carried that line and the notification dispatcher did not, so one
/// generator throwing on its own data cost the recipient the notification, and
/// the only trace left of the event was five warnings from the middleware.
///
/// Asserted against the sources: the parameters are built inside a background
/// service that needs a live broker to get that far, so nothing but the text of
/// the consumers says which queues carry work that has to outlive a failure.
/// </remarks>
public class DeadLetterRoutingShould
{
    /// <summary>How every Rabbit consumer in the solution declares its topology.</summary>
    private const string ConsumerParameters = "new RabbitConsumerParameters(";

    /// <summary>Assignment of the dead-letter exchange inside those parameters.</summary>
    private const string RoutesPoisonMessages = "DeadLetterExchange =";

    /// <summary>The single place that puts a dead-letter exchange on the broker.</summary>
    private const string DeclaresTheDestination = "DeadLetterQueue.DeclareTerminal(";

    /// <summary>
    /// The realtime push queue, exempt by decision rather than by omission: its
    /// message is a copy of a notification the dispatcher has already stored, and
    /// the client reads that over REST. A push kept past its moment gives nobody
    /// anything to act on.
    /// </summary>
    private const string ExemptConsumer = "RealtimeNotificationConsumer.cs";

    /// <summary>
    /// Walks up from the test binary to the repository root. The sources are not
    /// copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null &&
                   !(Directory.Exists(Path.Combine(directory.FullName, "src")) &&
                     Directory.Exists(Path.Combine(directory.FullName, "test"))))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return directory!.FullName;
        }
    }

    private static string[] Consumers => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
        .Where(IsAuthored)
        .Where(path => File.ReadAllText(path).Contains(ConsumerParameters, StringComparison.Ordinal))
        .ToArray();

    /// <summary>
    /// A rule that matches nothing passes. Three consumers exist — mail,
    /// notifications and the realtime push — so finding fewer means the search
    /// string went stale rather than that the tree is clean.
    /// </summary>
    [Fact]
    public void FindEveryConsumer() =>
        Consumers.Should().HaveCountGreaterOrEqualTo(3);

    [Fact]
    public void DeadLetterEveryQueueThatCarriesWork() =>
        Consumers
            .Where(path => Path.GetFileName(path) != ExemptConsumer)
            .Where(path => !File.ReadAllText(path).Contains(RoutesPoisonMessages, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEmpty(
                "a queue without a dead-letter exchange has the broker discard whatever its " +
                "consumer could not process, and a discarded event leaves nothing to restore " +
                "the notification, the letter, or even the fact that either was due");

    [Fact]
    public void DeclareWhateverIsDeadLetteredInto() =>
        Consumers
            .Where(path => File.ReadAllText(path).Contains(RoutesPoisonMessages, StringComparison.Ordinal))
            .Where(path => !File.ReadAllText(path).Contains(DeclaresTheDestination, StringComparison.Ordinal))
            .Select(path => Path.GetFileName(path))
            .Should().BeEmpty(
                "naming an exchange nobody declared loses the message just as quietly: the " +
                "broker discards what it cannot route out of the queue");

    /// <summary>
    /// The exemption has to keep naming a consumer that exists, or a rename turns
    /// it into a hole nobody sees.
    /// </summary>
    [Fact]
    public void KeepTheExemptionNamingAConsumer() =>
        Consumers.Select(path => Path.GetFileName(path)).Should().Contain(ExemptConsumer);

    private static bool IsAuthored(string path) =>
        !path
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "obj" or "bin" or "node_modules");
}
