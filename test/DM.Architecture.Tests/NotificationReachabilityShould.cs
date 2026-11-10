using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The queue binding is NotificationRoutingShould's; this is the other half. An event named anywhere
/// outside the dispatcher counts as sent. Asserted against the sources: publication is a call inside
/// a method body.
/// </remarks>
public class NotificationReachabilityShould
{

    // The dispatcher owns the answering side: naming an event there is not sending it.
    private const string DispatcherProject = "DM.Workers.NotificationDispatcher";

    // The tables every reader of a notification shares; each names events by the dozen and sends
    // none.
    private static readonly string[] SharedTables =
    {
        "NotificationCategoryMapper.cs",
        "NotificationWording.cs",
        "NotificationDestination.cs"
    };

    /// <summary>
    /// A qualified use of the event enum. The lookbehind keeps SecurityEventType
    /// out of the match: it is a different enum whose member names overlap.
    /// </summary>
    private static readonly Regex NamedEvent = new(
        @"(?<![A-Za-z])EventType\.([A-Za-z]\w*)", RegexOptions.Compiled);

    [Fact]
    public void SendEveryEventAGeneratorAnswers()
    {
        var answered = AnsweredEvents();
        answered.Should().NotBeEmpty(
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        var unreachable = answered
            .Except(NamedOutsideTheDispatcher())
            .ToArray();

        unreachable.Should().BeEmpty(
            "a generator whose event nobody sends is a feature finished everywhere except " +
            "where it starts, and it fails by staying silent");
    }

    private static IReadOnlyCollection<string> AnsweredEvents() => new SortedSet<string>(
        NotificationGenerators.AnsweredByAny().Select(candidate => candidate.ToString()), StringComparer.Ordinal);

    private static IReadOnlyCollection<string> NamedOutsideTheDispatcher()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(DispatcherProject, StringComparison.Ordinal))
            .Where(RepositoryFiles.IsAuthored)
            .Where(file => !SharedTables.Contains(Path.GetFileName(file), StringComparer.Ordinal))
            .ToArray();

        sources.Should().NotBeEmpty("the services that send events live under src/");

        return sources
            .SelectMany(file => NamedEvent.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }
}
