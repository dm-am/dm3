using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A notification the client turns into a link carries the field that link is
/// built from.
/// </summary>
/// <remarks>
/// The payload is an anonymous object on one side and an untyped bag on the
/// other. A generator writes BlogId, the API answers with blogId — the payload
/// is the one property serialized with a camel-case key policy — and the client
/// reads payload.blogId to decide where the notification points. Nothing between
/// the two ends is typed, so renaming the field in the generator compiles,
/// deploys and turns the notification into a line of text nobody can click.
/// Nothing fails and nothing is logged: the notification still arrives, with its
/// title and its wording and no way to reach the thing it is about.
///
/// The rule ties the two ends together by name instead. For every event the
/// client builds a link for, every notification carrying that event has to write
/// the field the client reads off it. Typing the bag is the other answer to the
/// same question, and it is a contract per generator across all of them; this is
/// one file, and it fails on the rename that matters.
///
/// Read from the sources on both sides, because there is no type to ask on
/// either: the payload is an anonymous object built inside a method body, and on
/// the client it arrives as an any. Which generator answers which event is asked
/// of the assembly, the way the processor asks it, and the answer is not always
/// what the notification carries — a subscription generator renames the event on
/// the way out. What a generator writes is read from its file, found by the name
/// of the type, so a generator whose file is named after something else fails the
/// rule instead of dropping out of it.
///
/// The client is also ready for events that nothing puts on a notification.
/// Those are listed by name: the day one of them starts arriving it falls under
/// the rule rather than staying an exception, and a branch added for an event
/// nobody raises has to be written down here instead of passing for a working
/// link.
/// </remarks>
public class NotificationLinkShould
{
    private const string GeneratorInterface = "INotificationGenerator";
    private const string ResolveMethod = "CanResolve";

    /// <summary>The generators, one file each, named after the type in it.</summary>
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Implementation/Notifiers";

    /// <summary>The one place the client turns a notification into a link.</summary>
    private const string LinkSource =
        "src/DM.Web.Client/src/entities/notification/lib/notificationLink.ts";

    /// <summary>The notification a generator hands back.</summary>
    private const string EmissionStart = "new CreateNotification";

    /// <summary>The end of a branch of the link table.</summary>
    private const string BranchEnd = "return";

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    /// <summary>"case NotificationType.X:" — an event the client answers for.</summary>
    private static readonly Regex FollowedEvent = new(
        @"case\s+NotificationType\.(\w+)\s*:", RegexOptions.Compiled);

    /// <summary>"payload.x" — a field the client reads off the notification.</summary>
    private static readonly Regex FollowedKey = new(
        @"payload\.([A-Za-z_]\w*)", RegexOptions.Compiled);

    /// <summary>The event a generator renames its notification to.</summary>
    private static readonly Regex RenamesTheEvent = new(
        @"^\s*EventType\s*=\s*EventType\.(\w+)", RegexOptions.Compiled);

    /// <summary>The member of the notification that carries the payload.</summary>
    private static readonly Regex WritesThePayload = new(
        @"^\s*Metadata\s*=\s*new\b", RegexOptions.Compiled);

    /// <summary>
    /// Events the client links and no notification carries. Three shapes, none of
    /// them a broken contract: NewGame, NewTopic and NewTopicComment are answered
    /// and renamed on the way out, so the subscription name is what arrives;
    /// BlogInvitationAccepted and BlogInvitationRejected are published by the blog
    /// module and answered by no generator; NewTopicInSubscribedBoard and
    /// NewPostInSubscribedGame are named by nobody but the client.
    /// </summary>
    private static readonly string[] NeverCarried =
    {
        "BlogInvitationAccepted",
        "BlogInvitationRejected",
        "NewGame",
        "NewPostInSubscribedGame",
        "NewTopic",
        "NewTopicComment",
        "NewTopicInSubscribedBoard"
    };

    private static readonly string[] GeneratorSources = Directory.GetFiles(
        Path.Combine(RepositoryRoot, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void CarryTheFieldTheClientBuildsTheLinkFrom()
    {
        var followed = KeysTheClientFollows();
        followed.Should().HaveCountGreaterThan(15,
            "a rule that matches nothing passes: the client links notifications of most kinds");

        var emissions = Emissions();
        emissions.Should().HaveCountGreaterThan(30,
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        emissions.SelectMany(emission => emission.Events).Intersect(followed.Keys)
            .Should().HaveCountGreaterThan(5,
                "a rule that matches nothing passes: most of what the client links is raised");

        var unwritten = new List<string>();
        foreach (var emission in emissions)
        {
            foreach (var carried in emission.Events.Where(followed.ContainsKey))
            {
                unwritten.AddRange(followed[carried]
                    .Select(Member)
                    .Except(emission.Keys, StringComparer.Ordinal)
                    .Select(key => $"{emission.Generator} carries {carried} without {key}"));
            }
        }

        unwritten.Should().BeEmpty(
            "the client reads the field by name and shows a notification it cannot " +
            "find one in as text nobody can follow, so a renamed field takes the link with it");
    }

    [Fact]
    public void KeepTheListOfEventsNobodyCarriesHonest()
    {
        var followed = KeysTheClientFollows();
        var carried = Emissions()
            .SelectMany(emission => emission.Events)
            .ToHashSet(StringComparer.Ordinal);

        NeverCarried.Except(followed.Keys).Should().BeEmpty(
            "an exemption for an event the client no longer links guards nothing " +
            "and outlives the reason it was written down");

        NeverCarried.Intersect(carried).Should().BeEmpty(
            "an event a notification started carrying has to leave the list, or the " +
            "field its link is built from is held by nobody");

        followed.Keys.Except(carried).Except(NeverCarried).Should().BeEmpty(
            "a branch of the link table no notification ever reaches is either a " +
            "feature that stopped halfway or a name that drifted, and the client " +
            "shows neither");
    }

    /// <summary>
    /// The field the client reads to build a link, by the event it reads it for.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyCollection<string>> KeysTheClientFollows()
    {
        var source = SourceText.ReadCode(Path.Combine(RepositoryRoot, LinkSource));
        var followed = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var pending = new List<string>();

        foreach (var line in source.Split('\n'))
        {
            var label = FollowedEvent.Match(line);
            if (label.Success)
            {
                pending.Add(label.Groups[1].Value);
                continue;
            }

            if (!line.Contains(BranchEnd, StringComparison.Ordinal))
            {
                continue;
            }

            var keys = FollowedKey.Matches(line)
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

            if (keys.Count > 0)
            {
                foreach (var followedEvent in pending)
                {
                    if (!followed.TryGetValue(followedEvent, out var read))
                    {
                        read = new SortedSet<string>(StringComparer.Ordinal);
                        followed.Add(followedEvent, read);
                    }

                    read.UnionWith(keys);
                }
            }

            pending.Clear();
        }

        FollowedEvent.Matches(source)
            .Select(match => match.Groups[1].Value)
            .Except(followed.Keys, StringComparer.Ordinal)
            .Should().BeEmpty(
                "a branch that reads nothing off the payload in the line it returns is " +
                "a link this rule cannot see, and an unseen branch is an unheld one");

        return followed.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Every notification the generators hand back: what it carries and what it
    /// is called by the time it leaves.
    /// </summary>
    private static IReadOnlyCollection<Emission> Emissions()
    {
        var emissions = new List<Emission>();
        foreach (var generator in Generators())
        {
            var source = SourceText.ReadCode(SourceOf(generator));
            var answered = AnsweredEvents(generator);

            var at = source.IndexOf(EmissionStart, StringComparison.Ordinal);
            while (at >= 0)
            {
                var opening = source.IndexOf('{', at);
                opening.Should().BeGreaterThan(at,
                    $"{generator.Name} hands its notification back as an object initializer");
                source[(at + EmissionStart.Length)..opening].Trim().Should().BeEmpty(
                    $"{generator.Name} fills its notification in somewhere this rule cannot read");

                var closing = ObjectInitializer.ClosingBrace(source, opening);
                var members = ObjectInitializer
                    .TopLevelParts(source.Substring(opening + 1, closing - opening - 1))
                    .ToArray();

                var rename = members
                    .Select(member => RenamesTheEvent.Match(member))
                    .FirstOrDefault(match => match.Success);

                emissions.Add(new Emission(
                    generator.Name,
                    rename is null
                        ? answered
                        : (IReadOnlyCollection<string>)new[] { rename.Groups[1].Value },
                    PayloadKeys(generator, members)));

                at = source.IndexOf(EmissionStart, closing, StringComparison.Ordinal);
            }
        }

        return emissions;
    }

    /// <summary>The fields of one payload, by the name the generator writes them under.</summary>
    private static IReadOnlyCollection<string> PayloadKeys(Type generator, IEnumerable<string> members)
    {
        var payload = members.SingleOrDefault(member => WritesThePayload.IsMatch(member));
        payload.Should().NotBeNull(
            $"{generator.Name} writes its payload in the initializer of the notification, " +
            "which is the only place a rule reading the sources can find one");

        var opening = payload!.IndexOf('{');
        opening.Should().BePositive($"{generator.Name} writes its payload as an initializer");

        var closing = ObjectInitializer.ClosingBrace(payload, opening);
        return ObjectInitializer
            .Members(payload.Substring(opening + 1, closing - opening - 1))
            .ToArray();
    }

    /// <summary>The file the generator is written in, found by the name of the type.</summary>
    private static string SourceOf(Type generator)
    {
        var files = GeneratorSources
            .Where(file => Path.GetFileNameWithoutExtension(file) == generator.Name)
            .ToArray();

        files.Should().ContainSingle(
            $"one file under the notifiers holds {generator.Name}: this rule reads the " +
            "payload out of the sources and finds them by the name of the type");

        return files[0];
    }

    private static IEnumerable<Type> Generators() => Dispatcher.GetTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(type => type.GetInterfaces().Any(contract => contract.Name == GeneratorInterface));

    /// <summary>
    /// The production answer, asked the way the processor asks it. Instances are
    /// left unconstructed on purpose: CanResolve reads a constant or a static
    /// table, while building a real generator would take a database.
    /// </summary>
    private static IReadOnlyCollection<string> AnsweredEvents(Type generator)
    {
        var canResolve = generator.GetMethod(ResolveMethod)!;
        var instance = RuntimeHelpers.GetUninitializedObject(generator);

        return Enum.GetValues<EventType>()
            .Where(candidate => (bool)canResolve.Invoke(instance, new object[] { candidate })!)
            .Select(candidate => candidate.ToString())
            .ToArray();
    }

    /// <summary>
    /// The member behind a key of the payload: the API writes the payload, and only
    /// the payload, with a camel-case key policy.
    /// </summary>
    private static string Member(string key) => char.ToUpperInvariant(key[0]) + key[1..];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    /// <summary>One notification a generator hands back.</summary>
    private sealed record Emission(
        string Generator,
        IReadOnlyCollection<string> Events,
        IReadOnlyCollection<string> Keys);
}
