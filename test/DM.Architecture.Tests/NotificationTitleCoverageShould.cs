using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// An event that can be sent is named in words.
/// </summary>
/// <remarks>
/// Both senders ask NotificationCategoryMapper first and return where it answers
/// null, then title what is left from NotificationText. A category is therefore
/// the permission to send, and the table has to cover every event that holds
/// one. Four did not. A changed topic, a changed publication, a filed ticket and
/// a granted award reached their recipient headed "Dungeon Master: Уведомление",
/// with the metadata under it and nothing anywhere naming what had happened. The
/// table was right about the other forty-three, which is why reading it top to
/// bottom shows nothing: what is missing from a table is not in it to be read.
///
/// The rule spans three projects that do not know each other. The mapper sorts
/// events in the domain, the words live in the worker, and the generators between
/// them decide which events can occur at all. No compiler looks across the three,
/// and a missing title is noticed first by the person it was owed to.
///
/// The sendable set is wider than the set of events a generator answers: a
/// subscription generator renames the event it puts on its notification, and the
/// renamed one is what the letter is titled by. That rename is an assignment
/// inside a method body, out of reach of reflection, so it is read from the
/// sources — the same way NotificationAudienceShould reads it.
/// </remarks>
public class NotificationTitleCoverageShould
{
    private const string SharedType =
        "DM.Workers.NotificationDispatcher.Implementation.NotificationText";

    private const string GeneratorInterface = "INotificationGenerator";
    private const string ResolveMethod = "CanResolve";

    /// <summary>The generators, whose sources carry the renames.</summary>
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Implementation/Notifiers";

    /// <summary>An outgoing event type assigned inside a CreateNotification.</summary>
    private static readonly Regex RenamesTheEvent = new(
        @"EventType\s*=\s*EventType\.(\w+)", RegexOptions.Compiled);

    /// <summary>The event a generator declares it answers.</summary>
    private static readonly Regex AnswersTheEvent = new(
        @"EventType\s+EventType\s*=>\s*EventType\.(\w+)", RegexOptions.Compiled);

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    /// <summary>
    /// Reached by name, because NotificationText is internal to the worker and
    /// this suite is not on its InternalsVisibleTo list.
    /// </summary>
    private static readonly MethodInfo TitleOf = Dispatcher
        .GetType(SharedType, throwOnError: true)!
        .GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void TitleEveryEventACategoryLetsOut()
    {
        var sendable = SendableEvents();
        sendable.Should().HaveCountGreaterThan(20,
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        var sent = sendable
            .Where(candidate => NotificationCategoryMapper.GetCategory(candidate) != null)
            .ToArray();
        sent.Should().NotBeEmpty(
            "a rule that matches nothing passes: a category is what lets an event out");

        var untitled = Title(EventType.Unknown);
        sent.Where(candidate => Title(candidate) == untitled)
            .Select(candidate => candidate.ToString())
            .Should().BeEmpty(
                "an unnamed event is delivered all the same: the subject line says only " +
                "that something happened, and the letter never says what");
    }

    /// <summary>
    /// Every event a generator can produce is either let out or deliberately
    /// kept in.
    /// </summary>
    /// <remarks>
    /// The other direction of the same rule, and the one that was missing. A
    /// category is the permission to leave the site, and an event absent from the
    /// table is refused it by omission — which looks exactly like an event
    /// somebody chose to keep in the application. Five had fallen out that way,
    /// each beside its own near neighbour in the same table.
    ///
    /// The exemption list is what makes "in the application only" a thing
    /// somebody decides rather than forgets. It is empty, and an empty list is
    /// the answer: every event the dispatcher can raise is worth telling its
    /// recipient about through the channels they asked for.
    /// </remarks>
    [Fact]
    public void SortEveryEventANotificationCanCarry()
    {
        // The three broadcasts of the global chat, and only those. Each leaves
        // UsersInterested empty on purpose — nothing is stored for anybody, the
        // API pushes them to every open connection and they are gone. A letter or
        // a bot message for one of them would be addressed to nobody, and the
        // firehose of a public chat is the last thing that belongs in either
        // channel. Both generators say so in their own remarks.
        var inApplicationOnly = new HashSet<EventType>
        {
            EventType.NewGlobalChatMessage,
            EventType.GlobalChatEventStarted,
            EventType.GlobalChatEventEnded
        };

        var carried = CarriedEvents();
        carried.Should().HaveCountGreaterThan(20,
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
    /// <remarks>
    /// Narrower than <see cref="SendableEvents" />, which unions both and is
    /// right for the title rule: a title is looked up for whatever the
    /// notification carries, and either kind can be it. A category, though, is
    /// asked about the carried event only, so counting triggers here would
    /// demand one for NewGame — an event no notification ever bears, because the
    /// generator answering it renames its notification to
    /// NewGameFromSubscribedAuthor.
    /// </remarks>
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
            Path.Combine(RepositoryRoot, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

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

    private static string Title(EventType eventType) =>
        (string)TitleOf.Invoke(null, new object[] { eventType })!;

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

    /// <summary>
    /// The production answer, asked the way the processor asks it. Instances are
    /// left unconstructed on purpose: CanResolve reads a constant or a static
    /// table, while building a real generator would take a database.
    /// </summary>
    private static IEnumerable<EventType> AnsweredEvents()
    {
        var generators = Dispatcher.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => type.GetInterfaces().Any(contract => contract.Name == GeneratorInterface));

        foreach (var generator in generators)
        {
            var canResolve = generator.GetMethod(ResolveMethod)!;
            var instance = RuntimeHelpers.GetUninitializedObject(generator);

            foreach (var candidate in Enum.GetValues<EventType>())
            {
                if ((bool)canResolve.Invoke(instance, new object[] { candidate })!)
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IReadOnlyCollection<EventType> RenamedEvents()
    {
        var sources = Directory.GetFiles(
            Path.Combine(RepositoryRoot, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

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
}
