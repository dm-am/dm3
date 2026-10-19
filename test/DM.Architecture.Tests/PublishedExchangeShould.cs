using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A publisher does not depend on somebody else having consumed first.
/// </summary>
/// <remarks>
/// Without a declaration the exchange exists only once a consumer declared it. Asserted on the
/// sources: the parameters are built behind a live broker.
/// </remarks>
public class PublishedExchangeShould
{
    /// <summary>How every Rabbit producer in the solution names its exchange.</summary>
    private static readonly Regex Publishes = new(
        @"new DmProducerParameters\(\s*([\w.]+)", RegexOptions.Compiled);

    /// <summary>
    /// How a host declares what it publishes to. The leading dot is what tells a
    /// call from the declaration of the extension method itself.
    /// </summary>
    private static readonly Regex Declares = new(
        @"\.AddDmPublishedExchanges\(([^;]*?)\)\s*;", RegexOptions.Compiled | RegexOptions.Singleline);

    [Fact]
    public void DeclareEveryExchangeSomethingPublishesTo()
    {
        var published = RepositoryFiles.ProductionSources()
            .SelectMany(path => Publishes.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        published.Should().NotBeEmpty(
            "the solution builds Rabbit producers, and a walk that finds none of them passes " +
            "whatever they publish to");

        var declared = DeclaredExchanges();
        declared.Should().NotBeEmpty(
            "the hosts declare what they publish to, and finding no declaration passes " +
            "whatever the producers do");

        published.Except(declared).Should().BeEmpty(
            "an exchange nothing declares exists only for as long as somebody has consumed " +
            "it, and a publish into one that is gone is refused by the broker without " +
            "reaching the code that published");
        declared.Except(published).Should().BeEmpty(
            "a host declaring an exchange it publishes nothing to is a name that outlived " +
            "its producer, and the next reader takes the list for the answer");
    }

    /// <summary>
    /// Waiting for the broker to take the message is asked for in exactly two
    /// places, each for its own reason, and nowhere else.
    /// </summary>
    /// <remarks>
    /// The letter owes the reader a refusal, and the relay's mark must follow the confirm. A third producer
    /// in a request would put every write behind the broker.
    /// </remarks>
    [Fact]
    public void WaitForTheBrokerOnlyWhereTheMessageIsTheWholeObligation()
    {
        var producers = RepositoryFiles.ProductionSources()
            .Where(path => Publishes.IsMatch(File.ReadAllText(path)))
            .ToList();

        producers.Should().HaveCountGreaterThanOrEqualTo(3,
            "three producers exist - the outbox relay, the mail queue and the realtime " +
            "push - so finding fewer means the search went stale");

        var waiting = producers
            .Where(path => File.ReadAllText(path).Contains("PublishConfirmTimeout", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        waiting.Should().Equal(["MailSender.cs", "OutboxRelayService.cs"],
            "the letter is the whole of what the request owes and is recoverable from " +
            "nothing, and the relay must not mark a row published on a publish the broker " +
            "never confirmed - while a producer inside a request waiting on the broker " +
            "fails requests whose work is done and buys a duplicate on the retry");
    }

    private static HashSet<string> DeclaredExchanges() => RepositoryFiles.ProductionSources()
        .SelectMany(path => Declares.Matches(File.ReadAllText(path)))
        .SelectMany(match => match.Groups[1].Value.Split(','))
        .Select(name => name.Trim())
        .Where(name => name.Length > 0)
        .ToHashSet(StringComparer.Ordinal);
}
