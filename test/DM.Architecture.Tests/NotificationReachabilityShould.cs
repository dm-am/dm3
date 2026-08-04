using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every event a notification generator answers is sent by somebody.
/// </summary>
/// <remarks>
/// The dispatcher derives its queue binding from the generators, so the broker
/// always reaches them, and NotificationRoutingShould holds that derivation in
/// place. The other half was held by nothing. Five generators — ban, warning,
/// ticket, account lockout, opened recruitment — answered events that no service
/// ever sent. Everything around them was finished: the category in the profile
/// settings, the subject of the letter, the text for the bot. Nothing failed,
/// nothing was logged, and not one notification could ever be produced.
///
/// The rule reads a naming, not a call: an event named anywhere outside the
/// dispatcher counts as sent. Which line sends it is for the reader to see; that
/// nobody anywhere names it is the state this guards against, and it is the state
/// five finished features sat in.
///
/// Asserted against the sources, because publication is a call inside a method
/// body and neither the type system nor the IL ties a producer to a generator.
/// </remarks>
public class NotificationReachabilityShould
{
    private const string GeneratorInterface = "INotificationGenerator";
    private const string ResolveMethod = "CanResolve";

    /// <summary>The dispatcher owns the answering side: naming an event there is not sending it.</summary>
    private const string DispatcherProject = "DM.Workers.NotificationDispatcher";

    /// <summary>
    /// Sorts every event type into a notification category. It names them all and
    /// sends none, so left in the scan it would vouch for every generator at once.
    /// </summary>
    private const string CategoryMapper = "NotificationCategoryMapper.cs";

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    /// <summary>
    /// A qualified use of the event enum. The lookbehind keeps SecurityEventType
    /// out of the match: it is a different enum whose member names overlap.
    /// </summary>
    private static readonly Regex NamedEvent = new(
        @"(?<![A-Za-z])EventType\.([A-Za-z]\w*)", RegexOptions.Compiled);

    /// <summary>
    /// Answered by a generator and deliberately never sent. Retired is the status
    /// a character ends up in, not something that happens to one: every transition
    /// into it publishes the reason instead — died, exiled, left — and those three
    /// are what reaches the generator.
    /// </summary>
    private static readonly string[] NeverSent = { "StatusCharacterRetired" };

    [Fact]
    public void SendEveryEventAGeneratorAnswers()
    {
        var answered = AnsweredEvents();
        answered.Should().HaveCountGreaterThan(20,
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        var unreachable = answered
            .Except(NamedOutsideTheDispatcher())
            .Except(NeverSent)
            .ToArray();

        unreachable.Should().BeEmpty(
            "a generator whose event nobody sends is a feature finished everywhere except " +
            "where it starts, and it fails by staying silent");
    }

    [Fact]
    public void KeepTheListOfSilentEventsHonest()
    {
        NeverSent.Except(AnsweredEvents()).Should().BeEmpty(
            "an exception for an event no generator answers guards nothing and outlives its reason");
        NeverSent.Intersect(NamedOutsideTheDispatcher()).Should().BeEmpty(
            "an event that is sent now must leave the list, or the next one to go quiet hides behind it");
    }

    /// <summary>
    /// The production answer, asked the way the consumer asks it. Instances are
    /// left unconstructed on purpose: CanResolve reads a constant or a static
    /// table, while building a real generator would take a database.
    /// </summary>
    private static IReadOnlyCollection<string> AnsweredEvents()
    {
        var answered = new SortedSet<string>(StringComparer.Ordinal);
        var generators = Dispatcher.GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => type.GetInterfaces().Any(contract => contract.Name == GeneratorInterface));

        foreach (var generator in generators)
        {
            var canResolve = generator.GetMethod(ResolveMethod)!;
            var events = canResolve.GetParameters()[0].ParameterType;
            var instance = RuntimeHelpers.GetUninitializedObject(generator);

            foreach (var candidate in Enum.GetValues(events))
            {
                if ((bool)canResolve.Invoke(instance, new[] { candidate })!)
                {
                    answered.Add(Enum.GetName(events, candidate)!);
                }
            }
        }

        return answered;
    }

    private static IReadOnlyCollection<string> NamedOutsideTheDispatcher()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Contains(DispatcherProject, StringComparison.Ordinal))
            .Where(file => !IsBuildOutput(file))
            .Where(file => Path.GetFileName(file) != CategoryMapper)
            .ToArray();

        sources.Should().NotBeEmpty("the services that send events live under src/");

        return sources
            .SelectMany(file => NamedEvent.Matches(File.ReadAllText(file)).Cast<Match>())
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Compilation output carries copies of the sources and would answer for them.
    /// </summary>
    private static bool IsBuildOutput(string file) => file
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin");

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
