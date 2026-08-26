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
/// The client has a call that declares a producer's exchange and never makes it,
/// so the exchanges of this system existed only because the consumers declare
/// what they subscribe to. On a stand where the mail worker had never started,
/// every letter was published to an exchange that was not there — and with no
/// confirmation asked for, the broker's refusal reached nobody at all: the
/// account was written, the request answered 200, and the reader waited for an
/// activation letter that had never been queued.
///
/// Asserted on the sources, because the parameters are built inside hosts that
/// need a live broker to reach that line, and the declaration is a hosted service
/// that needs one too.
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
    /// The letter is what a registration or a password reset owes the reader, and
    /// nothing left behind can reconstruct it, so a refusal there has to reach the
    /// caller. The outbox relay waits for the opposite reason: the row it is about
    /// to mark published is the durable record of the event, and a mark without
    /// the broker's confirm would record "delivered" about a message the broker
    /// may not have taken — the silent loss coming back through another door. The
    /// wait is affordable there because the relay is background work: the rule
    /// "nobody waits on the bus" protected an HTTP caller the relay does not have.
    ///
    /// Both directions, because the interesting failure is a third producer
    /// acquiring the wait: it is one property on a parameters object, and adding
    /// it to a producer inside a request reads like making things safer while
    /// quietly moving every write in the site behind the availability of the
    /// broker.
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
