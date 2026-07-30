using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
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

    /// <summary>"Name = expression": a member of the payload spelled out.</summary>
    private static readonly Regex NamedMember = new(
        @"^\s*([A-Za-z_]\w*)\s*=(?!=)", RegexOptions.Compiled);

    /// <summary>"source.Name": a member that borrows its name from the source.</summary>
    private static readonly Regex BorrowedMember = new(
        @"\.\s*([A-Za-z_]\w*)\s*$", RegexOptions.Compiled);

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

                var closing = ClosingBrace(source, opening);
                foreach (var member in Members(source.Substring(opening + 1, closing - opening - 1)))
                {
                    keys.Add(member);
                }

                at = source.IndexOf(PayloadAssignment, closing, StringComparison.Ordinal);
            }
        }

        return keys;
    }

    /// <summary>
    /// Members of one object initializer, both spellings of a member.
    /// </summary>
    private static IEnumerable<string> Members(string body)
    {
        foreach (var member in TopLevelParts(body))
        {
            var named = NamedMember.Match(member);
            if (named.Success)
            {
                yield return named.Groups[1].Value;
                continue;
            }

            var borrowed = BorrowedMember.Match(member.Trim());
            if (borrowed.Success)
            {
                yield return borrowed.Groups[1].Value;
            }
        }
    }

    /// <summary>
    /// Splits an initializer at the commas that belong to it, leaving alone those
    /// inside a nested call, collection or literal.
    /// </summary>
    private static IEnumerable<string> TopLevelParts(string body)
    {
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var symbol = body[i];
            if (symbol is '"' or '\'')
            {
                i = EndOfLiteral(body, i);
            }
            else if (symbol is '{' or '(' or '[')
            {
                depth++;
            }
            else if (symbol is '}' or ')' or ']')
            {
                depth--;
            }
            else if (symbol == ',' && depth == 0)
            {
                yield return body.Substring(start, i - start);
                start = i + 1;
            }
        }

        yield return body.Substring(start);
    }

    /// <summary>Index of the brace that closes the one opened at the given position.</summary>
    private static int ClosingBrace(string source, int opening)
    {
        var depth = 0;
        for (var i = opening; i < source.Length; i++)
        {
            var symbol = source[i];
            if (symbol is '"' or '\'')
            {
                i = EndOfLiteral(source, i);
            }
            else if (symbol == '{')
            {
                depth++;
            }
            else if (symbol == '}' && --depth == 0)
            {
                return i;
            }
        }

        throw new InvalidOperationException("A notification payload is left open.");
    }

    /// <summary>
    /// Index of the quote that closes the literal opened at the given position, so
    /// that a brace or a comma inside it is not read as punctuation.
    /// </summary>
    private static int EndOfLiteral(string source, int opening)
    {
        var quote = source[opening];
        for (var i = opening + 1; i < source.Length; i++)
        {
            if (source[i] == '\\')
            {
                i++;
            }
            else if (source[i] == quote)
            {
                return i;
            }
        }

        return source.Length - 1;
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
