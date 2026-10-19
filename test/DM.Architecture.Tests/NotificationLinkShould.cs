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
/// A notification the server turns into a link carries the field that link is
/// built from.
/// </summary>
/// <remarks>
/// The payload is an anonymous object on one side and an untyped bag on the other, so renaming a
/// field turns the link into plain text. The table is asked directly, the generators read from
/// their files, which generator answers an event asked of the assembly. A destination no
/// notification reaches fails too.
/// </remarks>
public class NotificationLinkShould
{

    /// <summary>The generators, one file each, named after the type in it.</summary>
    private const string NotifiersDirectory =
        "src/DM.Workers.NotificationDispatcher/Notifiers";

    /// <summary>The notification a generator hands back.</summary>
    private const string EmissionStart = "new CreateNotification";

    /// <summary>The event a generator renames its notification to.</summary>
    private static readonly Regex RenamesTheEvent = new(
        @"^\s*EventType\s*=\s*EventType\.(\w+)", RegexOptions.Compiled);

    /// <summary>The member of the notification that carries the payload.</summary>
    private static readonly Regex WritesThePayload = new(
        @"^\s*Metadata\s*=\s*new\b", RegexOptions.Compiled);

    private static readonly string[] GeneratorSources = Directory.GetFiles(
        Path.Combine(RepositoryLayout.Root, NotifiersDirectory), "*.cs", SearchOption.AllDirectories);

    [Fact]
    public void CarryTheFieldTheLinkIsBuiltFrom()
    {
        var followed = FieldsTheTableFollows();
        followed.Should().NotBeEmpty(
            "a rule that matches nothing passes: the table links notifications of most kinds");

        var emissions = Emissions();
        emissions.Should().NotBeEmpty(
            "a rule that matches nothing passes: the dispatcher carries dozens of generators");

        emissions.SelectMany(emission => emission.Events).Intersect(followed.Keys)
            .Should().NotBeEmpty(
                "a rule that matches nothing passes: most of what the table links is raised");

        var unwritten = new List<string>();
        foreach (var emission in emissions)
        {
            foreach (var carried in emission.Events.Where(followed.ContainsKey))
            {
                // The table finds the field whatever its case, so this does too.
                if (!emission.Keys.Contains(followed[carried], StringComparer.OrdinalIgnoreCase))
                {
                    unwritten.Add($"{emission.Generator} carries {carried} without {followed[carried]}");
                }
            }
        }

        unwritten.Should().BeEmpty(
            "the table reads the field by name and leaves a notification it cannot find " +
            "one in as text nobody can follow, so a renamed field takes the link with it");
    }

    [Fact]
    public void LinkOnlyEventsANotificationCarries()
    {
        var carried = Emissions()
            .SelectMany(emission => emission.Events)
            .ToHashSet(StringComparer.Ordinal);

        FieldsTheTableFollows().Keys.Except(carried).Should().BeEmpty(
            "a row of the table no notification ever reaches is either a feature that " +
            "stopped halfway or a name that drifted, and nobody is ever sent there");
    }

    /// <summary>
    /// The field the table reads to build a link, by the event it reads it for.
    /// </summary>
    private static IReadOnlyDictionary<string, string> FieldsTheTableFollows()
    {
        var followed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var eventType in Enum.GetValues<EventType>())
        {
            var field = NotificationDestination.FieldOf(eventType);
            if (field != null)
            {
                followed.Add(eventType.ToString(), field);
            }
        }

        return followed;
    }

    /// <summary>
    /// Every notification the generators hand back: what it carries and what it
    /// is called by the time it leaves.
    /// </summary>
    private static IReadOnlyCollection<Emission> Emissions()
    {
        var emissions = new List<Emission>();
        foreach (var generator in NotificationGenerators.Types)
        {
            var source = SourceText.ReadCode(SourceOf(generator));
            var answered = NotificationGenerators.Answered(generator).Select(candidate => candidate.ToString()).ToArray();

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

    /// <summary>One notification a generator hands back.</summary>
    private sealed record Emission(
        string Generator,
        IReadOnlyCollection<string> Events,
        IReadOnlyCollection<string> Keys);
}
