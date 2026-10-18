using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Domain.Core.Enums;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One event is called one thing, in the list and in the letter about it, and the
/// client calls it nothing at all.
/// </summary>
/// <remarks>
/// Every outbound channel takes its wording from NotificationWording, and the
/// notification list used to take its own from a table on the client. The two
/// tables drifted on fourteen events at once: the like the list called "Лайк
/// топика" arrived by mail as "Лайк на топик", and a blog invitation the list
/// named had no subject line at all and went out titled "Уведомление". Later the
/// client table was kept a subset of the server one and paired with it from here,
/// and the list still called 35 of 59 events "Уведомление", because nobody
/// remembered the client when the server learned a new event.
///
/// So the list row arrives titled: the server writes the title from the same
/// table the letter is written from, and the client shows it. What is left to
/// hold is the boundary itself. No client source gives a word to an event, in
/// the shapes a second table took (a row keyed by the enum, a switch arm that
/// answers one with a literal) or under another name (a table keyed by the
/// server's event names). Two more rules hold the vocabulary the client still
/// needs to react to a push: it spells an event the way the server spells it,
/// and spells it once. That pair answers a second, numeric copy of the enum that
/// lived in shared/api, where NewPoll had drifted to 51 — the server's slot for
/// DeletedPublicationComment — and the wire it was written against, where the
/// hub sent the event as its number while the list sent its name.
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
    /// A word given to an event through the enum: a table row [NotificationType.X]:
    /// "..." or a switch arm that answers one with a literal. Both are shapes the
    /// drift actually took. Naming an event to decide what to do about it is not
    /// one of them, and a screen reacting to a push does exactly that.
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

    /// <summary>Any TypeScript enum: its name and its body.</summary>
    private static readonly Regex TypeScriptEnum = new(
        @"enum\s+(\w+)\s*\{([^}]*)\}", RegexOptions.Compiled);

    /// <summary>One member of an enum body: the name and the value written for it.</summary>
    private static readonly Regex EnumMember = new(
        @"^\s*(\w+)\s*=\s*([^,\r\n]+?),?\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A line comment, dropped so a commented-out member is not read as one.</summary>
    private static readonly Regex LineComment = new(@"//[^\r\n]*", RegexOptions.Compiled);

    /// <summary>
    /// The shapes the client tables were written in, kept here so the two patterns
    /// above are shown to find them: a rule that matches nothing passes, and the
    /// tables they were written against are gone.
    /// </summary>
    private const string FormerTitleTable = """
        const TITLES: Partial<Record<NotificationType, string>> = {
          [NotificationType.LikedTopic]: "Лайк на топик",
        };
        switch (type) {
          case NotificationType.BanIssued: return "Выдан бан";
        }
        const RENAMED = {
          NewTopic: "Новый топик на форуме",
          LikedTopic: "Лайк на топик",
          BanIssued: "Выдан бан",
        };
        """;

    /// <summary>What a screen reacting to a push writes, which is no title.</summary>
    private const string PushReaction = """
        if (notification.eventType === NotificationType.NewMessage) void refresh();
        switch (notification.eventType) {
          case NotificationType.GlobalChatEventStarted:
            onEvent(notification.payload);
            break;
        }
        const routes = { name: "notifications", title: "Уведомления" };
        """;

    [Fact]
    public void KnowTheShapesOfATitleTable()
    {
        TitlesAnEvent.Matches(FormerTitleTable).Should().HaveCount(2,
            "the row keyed by the enum and the switch arm are the two shapes the first table took");
        WordedRows(FormerTitleTable).Should().BeEquivalentTo(
            new[] { "NewTopic", "LikedTopic", "BanIssued" },
            "a table keyed by the server's names is the same table under another name");

        TitlesAnEvent.IsMatch(PushReaction).Should().BeFalse(
            "naming an event to react to it gives it no word");
        WordedRows(PushReaction).Should().BeEmpty(
            "an object keyed by words that are not events is not a table of titles");
    }

    [Fact]
    public void TitleNoEventOnTheClient()
    {
        var root = RepositoryRoot;
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

    /// <summary>
    /// The client enum carries names now, not numbers, and every name is one the
    /// server answers to.
    /// </summary>
    /// <remarks>
    /// Numbers were the drift: one copy of the vocabulary gave NewPoll the value
    /// 51, which on the server is DeletedPublicationComment, and nothing in either
    /// tier could notice. With the value spelled as the member's own name the whole
    /// class of that mistake is gone — a name either exists on the server or it
    /// does not, and this test is where that is decided.
    /// </remarks>
    [Fact]
    public void SpellAnEventTheWayTheServerSpellsIt()
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot, ClientEventTable));
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

    /// <summary>
    /// The vocabulary is written down once on the client.
    /// </summary>
    /// <remarks>
    /// The rule is stated by content rather than by file name: a copy renamed on
    /// its way in is still a copy, and the one that existed was called EventType.
    /// An enum with three or more members the server answers to is the vocabulary,
    /// whatever it calls itself, and there is one place for it.
    /// </remarks>
    [Fact]
    public void SpellTheServerEventsInOnePlaceOnly()
    {
        var root = RepositoryRoot;
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
    /// bare name. Unknown is left out: it is the fallback, not an event.
    /// </summary>
    private static List<string> WordedRows(string source) =>
        WordedRow.Matches(LineComment.Replace(source, string.Empty))
            .Select(row => row.Groups[1].Value)
            .Where(name => name != nameof(EventType.Unknown)
                && Enum.TryParse<EventType>(name, false, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    /// <summary>Members of the named enum in the given source.</summary>
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

    /// <summary>Members of one enum body: the name and the value written for it.</summary>
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
    internal static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            var extension = Path.GetExtension(file);
            if ((extension == ".ts" || extension == ".vue")
                && !file.EndsWith(".spec.ts", StringComparison.Ordinal)
                && !file.EndsWith(".d.ts", StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            foreach (var file in SourceFiles(nested))
            {
                yield return file;
            }
        }
    }

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
