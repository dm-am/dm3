using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every field a notification carries is either named in Russian or not shown.
/// </summary>
/// <remarks>
/// A letter and a bot message are built by walking the metadata object and
/// printing every property of it. The table of Russian labels covered fifteen
/// keys out of the fifty-four the generators write, so the rest went out under
/// the name the C# property has — "LikerUsername", "AwardTier", "TokenId" — and
/// four of the fifteen it did cover were identifiers, printed as the slug they
/// are next to the human name of the same thing. Nothing failed: the formatter
/// falls back to the key. The fallback is invisible from the generator that adds
/// a field, which is why a new field goes out untranslated by default.
///
/// Asserted against the sources. The payload is an anonymous object built inside
/// a method body; the compiler does turn it into a type, but it does the same
/// with every EF projection and every request body in this assembly, and a
/// letter is not written from those.
/// </remarks>
public class NotificationMetadataShould
{
    private const string SharedType =
        "DM.Workers.NotificationDispatcher.Implementation.NotificationText";

    /// <summary>The project that owns both the generators and the words.</summary>
    private static readonly string DispatcherSource =
        Path.Combine("src", "DM.Workers.NotificationDispatcher");

    /// <summary>The assignment every payload is written through.</summary>
    private const string PayloadAssignment = "Metadata = new";

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    private static readonly Type Wording = Dispatcher.GetType(SharedType, throwOnError: true)!;

    private static readonly MethodInfo LabelOf =
        Wording.GetMethod("FormatPropertyName", BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo HiddenFrom =
        Wording.GetMethod("IsHiddenFromText", BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void NameEveryFieldItShowsTheReader()
    {
        var keys = PayloadKeys();
        keys.Should().HaveCountGreaterThan(40,
            "a rule that matches nothing passes: the generators write dozens of fields");

        var undecided = keys
            .Where(key => Label(key) == key)
            .Where(key => !Hidden(key))
            .ToArray();

        undecided.Should().BeEmpty(
            "a field with no Russian name is printed under its C# one, and a letter " +
            "of those reads as a dump of the record it was built from");
    }

    [Fact]
    public void KeepTheListOfHiddenFieldsHonest()
    {
        var keys = PayloadKeys();

        keys.Where(Hidden).Should().NotBeEmpty(
            "a rule that matches nothing passes: identifiers are carried and not shown");

        keys.Where(key => Hidden(key) && Label(key) != key).Should().BeEmpty(
            "a name for a field nobody is ever shown is wording that cannot be read " +
            "and will not be corrected");

        HiddenFields().Except(keys).Should().BeEmpty(
            "a field hidden from every channel that no generator writes guards " +
            "nothing and outlives the reason it was listed");
    }

    private static string Label(string key) => (string)LabelOf.Invoke(null, new object[] { key })!;

    private static bool Hidden(string key) => (bool)HiddenFrom.Invoke(null, new object[] { key })!;

    /// <summary>
    /// The fields the channels carry and never print, matched by type rather than
    /// by name the way the title table is: the list is private and this suite is
    /// not on the worker's InternalsVisibleTo list.
    /// </summary>
    private static IReadOnlyCollection<string> HiddenFields()
    {
        var lists = Wording
            .GetFields(BindingFlags.Static | BindingFlags.Public |
                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(field => field.FieldType == typeof(HashSet<string>))
            .ToArray();

        lists.Should().ContainSingle("the fields nobody is shown are listed in one place");
        return (HashSet<string>)lists[0].GetValue(null)!;
    }

    /// <summary>
    /// Every property name a generator writes into a notification payload.
    /// </summary>
    private static IReadOnlyCollection<string> PayloadKeys()
    {
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryRoot, DispatcherSource), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
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

    /// <summary>
    /// Compilation output carries copies of the sources and would answer for them.
    /// </summary>
    private static bool IsBuildOutput(string file) => file
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin");

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
