using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DM.Domain.Personal.Features.Notifications;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every field a notification carries is either named in Russian or not shown.
/// </summary>
/// <remarks>
/// A letter and a bot message print every property of the metadata, falling back to the key.
/// Asserted against the sources: the payload is an anonymous object inside a method body.
/// </remarks>
public class NotificationMetadataShould
{
    /// <summary>The project that owns the generators.</summary>
    private static readonly string DispatcherSource =
        Path.Combine("src", "DM.Workers.NotificationDispatcher");

    /// <summary>The assignment every payload is written through.</summary>
    private const string PayloadAssignment = "Metadata = new";

    [Fact]
    public void NameEveryFieldItShowsTheReader()
    {
        var keys = PayloadKeys();
        keys.Should().NotBeEmpty(
            "a rule that matches nothing passes: the generators write dozens of fields");

        var undecided = keys
            .Where(key => Label(key) == key)
            .Where(key => !Hidden(key))
            .ToArray();

        undecided.Should().BeEmpty(
            "a field with no Russian name is printed under its C# one, and a letter " +
            "of those reads as a dump of the record it was built from");
    }

    private static string Label(string key) => NotificationWording.FormatPropertyName(key);

    private static bool Hidden(string key) => NotificationWording.IsHiddenFromText(key);

    /// <summary>
    /// Every property name a generator writes into a notification payload.
    /// </summary>
    private static IReadOnlyCollection<string> PayloadKeys()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryLayout.Root, DispatcherSource), "*.cs", SearchOption.AllDirectories)
            .Where(RepositoryFiles.IsAuthored)
            .ToArray();

        sources.Should().NotBeEmpty("the generators live under the dispatcher project");

        var keys = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in sources)
        {
            var source = File.ReadAllText(file);
            var at = source.IndexOf(PayloadAssignment, StringComparison.Ordinal);
            while (at >= 0)
            {
                var opening = source.IndexOf('{', at);
                opening.Should().BeGreaterThan(at, "an anonymous payload opens with a brace");

                var closing = ObjectInitializer.ClosingBrace(source, opening);
                foreach (var member in ObjectInitializer.Members(
                             source.Substring(opening + 1, closing - opening - 1)))
                {
                    keys.Add(member);
                }

                at = source.IndexOf(PayloadAssignment, closing, StringComparison.Ordinal);
            }
        }

        return keys;
    }
}
