using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An event that can be sent is named in words.
/// </summary>
/// <remarks>
/// Both senders return where NotificationCategoryMapper answers null and title the rest from
/// NotificationWording, so a category is the permission to send. The renamed events of the
/// subscription generators are read from the sources, as NotificationAudienceShould reads them.
/// </remarks>
public class NotificationTitleCoverageShould
{

    /// <summary>The generators, whose sources carry the renames.</summary>
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Notifiers";

    /// <summary>An outgoing event type assigned inside a CreateNotification.</summary>
    private static readonly Regex RenamesTheEvent = new(
        @"EventType\s*=\s*EventType\.(\w+)", RegexOptions.Compiled);

    /// <summary>The event a generator declares it answers.</summary>
    private static readonly Regex AnswersTheEvent = new(
        @"EventType\s+EventType\s*=>\s*EventType\.(\w+)", RegexOptions.Compiled);

    [Fact]
    public void TitleEveryEventACategoryLetsOut()
    {
        var sendable = SendableEvents();
        sendable.Should().NotBeEmpty(
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        var sent = sendable
            .Where(candidate => NotificationCategoryMapper.GetCategory(candidate) != null)
            .ToArray();
        sent.Should().NotBeEmpty(
            "a rule that matches nothing passes: a category is what lets an event out");

        var untitled = NotificationWording.GetTitle(EventType.Unknown);
        sent.Where(candidate => NotificationWording.GetTitle(candidate) == untitled)
            .Select(candidate => candidate.ToString())
            .Should().BeEmpty(
                "an unnamed event is delivered all the same: the subject line says only " +
                "that something happened, and the letter never says what");
    }

    /// <summary>
    /// Every event a generator can produce is either let out or deliberately
    /// kept in.
    /// </summary>
    /// <remarks>An event absent from the table is refused by omission; staying in the application is decided below.</remarks>
    [Fact]
    public void SortEveryEventANotificationCanCarry()
    {
        // The three broadcasts of the global chat: pushed to every connection, stored for nobody.
        var inApplicationOnly = new HashSet<EventType>
        {
            EventType.NewGlobalChatMessage,
            EventType.GlobalChatEventStarted,
            EventType.GlobalChatEventEnded
        };

        var carried = CarriedEvents();
        carried.Should().NotBeEmpty(
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        carried
            .Where(candidate => NotificationCategoryMapper.GetCategory(candidate) == null)
            .Where(candidate => !inApplicationOnly.Contains(candidate))
            .Select(candidate => candidate.ToString())
            .Should().BeEmpty(
                "an event with no category is delivered in the application and nowhere " +
                "else, and nothing distinguishes that from a decision to keep it there");
    }

    /// <summary>
    /// The events that end up ON a notification, as against the ones that merely
    /// wake a generator up.
    /// </summary>
    /// <remarks>Narrower than <see cref="SendableEvents" />: a category is asked about the carried event only.</remarks>
    private static IReadOnlyCollection<EventType> CarriedEvents()
    {
        var renamed = RenamedEvents();
        var carried = new SortedSet<EventType>(renamed);

        // A generator that renames carries the new name and nothing else; one
        // that does not carries the event it answered.
        foreach (var answered in AnsweredEvents())
        {
            if (!TriggersOnly().Contains(answered))
            {
                carried.Add(answered);
            }
        }

        return carried;
    }

    /// <summary>
    /// Events a generator answers and then renames away, so no notification ever
    /// carries them.
    /// </summary>
    private static IReadOnlyCollection<EventType> TriggersOnly()
    {
        var sources = Directory.GetFiles(
            Path.Combine(RepositoryLayout.Root, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

        var triggers = new SortedSet<EventType>();
        foreach (var file in sources)
        {
            var source = File.ReadAllText(file);
            if (!RenamesTheEvent.IsMatch(source))
            {
                continue;
            }

            foreach (Match match in AnswersTheEvent.Matches(source))
            {
                if (Enum.TryParse<EventType>(match.Groups[1].Value, false, out var answered))
                {
                    triggers.Add(answered);
                }
            }
        }

        return triggers;
    }

    /// <summary>
    /// Every event a notification can carry out: the ones a generator answers and
    /// the ones a generator renames its notification to.
    /// </summary>
    private static IReadOnlyCollection<EventType> SendableEvents()
    {
        var sendable = new SortedSet<EventType>(AnsweredEvents());
        sendable.UnionWith(RenamedEvents());
        return sendable;
    }

    private static IEnumerable<EventType> AnsweredEvents() => NotificationGenerators.AnsweredByAny();

    private static IReadOnlyCollection<EventType> RenamedEvents()
    {
        var sources = Directory.GetFiles(
            Path.Combine(RepositoryLayout.Root, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

        sources.Should().NotBeEmpty("the generators live under the notifiers directory");

        var renamed = new SortedSet<EventType>();
        foreach (var match in sources
                     .SelectMany(file => RenamesTheEvent.Matches(File.ReadAllText(file)).Cast<Match>()))
        {
            if (Enum.TryParse<EventType>(match.Groups[1].Value, false, out var outgoing))
            {
                renamed.Add(outgoing);
            }
        }

        return renamed;
    }
}
