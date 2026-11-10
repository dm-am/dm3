using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The server titles the list row from the table the letter is written from, and the client shows it.
/// Held here: no client source gives a word to an event, and the client spells an event the way the
/// server does, once.
/// </remarks>
public class NotificationVocabularyShould
{
    private static readonly string ClientSource =
        Path.Combine("src", "DM.Web.Client", "src");

    /// <summary>The one place the client is allowed to spell the server's events.</summary>
    private static readonly string ClientEventTable =
        Path.Combine(ClientSource, "shared", "api", "models", "notifications", "index.ts");

    /// <summary>
    /// How many of an enum's members have to be server events before it counts as
    /// a copy of the vocabulary rather than a coincidence of naming.
    /// </summary>
    private const int MembersThatMakeACopy = 3;

    /// <summary>
    /// A word given to an event through the enum: a table row [NotificationType.X]: "..." or a switch arm
    /// answering one with a literal. Naming an event to react to a push is not one.
    /// </summary>
    private static readonly Regex TitlesAnEvent = new(
        @"NotificationType\.\w+\s*\]?\s*:\s*(return\s+)?[""'`]", RegexOptions.Compiled);

    /// <summary>
    /// A row of a table keyed by an event's bare name and answered with words:
    /// NewTopic: "Новый топик". Read per line, and counted against the server's
    /// names, because a key alone is any identifier.
    /// </summary>
    private static readonly Regex WordedRow = new(
        @"^\s*[""']?(\w+)[""']?\s*:\s*[""'`]\s*[А-Яа-я]", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex TypeScriptEnum = new(
        @"enum\s+(\w+)\s*\{([^}]*)\}", RegexOptions.Compiled);

    private static readonly Regex EnumMember = new(
        @"^\s*(\w+)\s*=\s*([^,\r\n]+?),?\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    // A line comment, dropped so a commented-out member is not read as one.
    private static readonly Regex LineComment = new(@"//[^\r\n]*", RegexOptions.Compiled);

    [Fact]
    public void TitleNoEventOnTheClient()
    {
        var root = RepositoryLayout.Root;
        var files = SourceFiles(Path.Combine(root, ClientSource)).ToArray();
        files.Should().HaveCountGreaterThan(100,
            "a rule that reads nothing passes: the client has hundreds of sources");

        var titling = new List<string>();
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (TitlesAnEvent.IsMatch(source))
            {
                titling.Add($"{relative}: a word given to NotificationType");
            }

            var rows = WordedRows(source);
            if (rows.Count >= MembersThatMakeACopy)
            {
                titling.Add($"{relative}: words for {string.Join(", ", rows)}");
            }
        }

        titling.Should().BeEmpty(
            "the list row arrives titled from the table the letter is written from; a " +
            "word for an event on the client is a second table, and the second table is " +
            "how one event came to have two names and 35 events none");
    }

    // A name either exists on the server or it does not; numbers drifted.
    [Fact]
    public void SpellAnEventTheWayTheServerSpellsIt()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryLayout.Root, ClientEventTable));
        var members = MembersOf(source, "NotificationType");

        members.Should().HaveCountGreaterThan(10,
            "a rule that matches nothing passes: the client acts on more than ten events");

        var drift = new List<string>();
        foreach (var (name, value) in members)
        {
            if (!Enum.TryParse<EventType>(name, false, out _))
            {
                drift.Add($"{name}: the server has no event of that name");
            }
            else if (value != $"\"{name}\"")
            {
                drift.Add($"{name}: arrives as \"{name}\", read here as {value}");
            }
        }

        drift.Should().BeEmpty(
            "both transports write the event as its name — MVC and the hub share one " +
            "JsonStringEnumConverter — so a member holding a number, or a name the " +
            "server never sends, matches nothing that ever arrives");
    }

    // By content, not by file name: an enum with three or more server events is the vocabulary.
    [Fact]
    public void SpellTheServerEventsInOnePlaceOnly()
    {
        var root = RepositoryLayout.Root;
        var copies = new List<string>();

        foreach (var file in SourceFiles(Path.Combine(root, ClientSource)))
        {
            foreach (Match declaration in TypeScriptEnum.Matches(File.ReadAllText(file)))
            {
                var known = Members(declaration.Groups[2].Value)
                    .Count(member => Enum.TryParse<EventType>(member.Name, false, out _));
                if (known >= MembersThatMakeACopy)
                {
                    copies.Add(Path.GetRelativePath(root, file).Replace('\\', '/'));
                }
            }
        }

        copies.Should().BeEquivalentTo(new[] { ClientEventTable.Replace('\\', '/') },
            "the vocabulary drifted precisely because it was written twice, and the " +
            "second copy is where NewPoll came to mean DeletedPublicationComment");
    }

    /// <summary>
    /// The server's event names a source gives words to, one per row keyed by the
    /// bare name. Unknown is left out: it is the fallback, not an event. So is a
    /// number: Enum.TryParse reads one as the member of that value, and a table
    /// keyed by places or tiers is no table of events.
    /// </summary>
    private static List<string> WordedRows(string source) =>
        WordedRow.Matches(LineComment.Replace(source, string.Empty))
            .Select(row => row.Groups[1].Value)
            .Where(name => name != nameof(EventType.Unknown)
                && !int.TryParse(name, out _)
                && Enum.TryParse<EventType>(name, false, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static List<(string Name, string Value)> MembersOf(string source, string name)
    {
        foreach (Match declaration in TypeScriptEnum.Matches(source))
        {
            if (declaration.Groups[1].Value == name)
            {
                return Members(declaration.Groups[2].Value);
            }
        }

        throw new InvalidOperationException($"the client declares no enum {name}");
    }

    private static List<(string Name, string Value)> Members(string body)
    {
        var members = new List<(string, string)>();
        foreach (Match member in EnumMember.Matches(LineComment.Replace(body, string.Empty)))
        {
            members.Add((member.Groups[1].Value, member.Groups[2].Value.Trim()));
        }

        return members;
    }

    /// <summary>
    /// Every client file that can render text. A spec is not text on a screen,
    /// and it may well have to name an event to assert about one.
    /// </summary>
    internal static IEnumerable<string> SourceFiles(string directory) => RepositoryFiles
        .Files(directory, "*", [])
        .Where(file => Path.GetExtension(file) is ".ts" or ".vue"
                       && !file.EndsWith(".spec.ts", StringComparison.Ordinal)
                       && !file.EndsWith(".d.ts", StringComparison.Ordinal));
}
