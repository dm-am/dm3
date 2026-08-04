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
/// One event is called one thing, in the list and in the letter about it.
/// </summary>
/// <remarks>
/// Every outbound channel takes its wording from NotificationText, and the
/// notification list took its own from a switch on the page. The two tables had
/// drifted on fourteen events at once: the like the list called "Лайк топика"
/// arrived by mail as "Лайк на топик", and a blog invitation the list named had
/// no subject line at all and went out titled "Уведомление". Nothing links the
/// two files and no compiler can, so the reader was the first place the two
/// names met.
///
/// Neither tier can import the other, so the pair is held from outside: the
/// client table is read from its source and put to GetTitle. The rest of the
/// suite is what makes that pairing worth having. One test keeps the words
/// inside the notification slice, so there is nowhere else for a second set of
/// them to appear. Two more hold the vocabulary they are keyed by: the client
/// spells an event the way the server spells it, and spells it once. That pair
/// answers a second, numeric copy of the enum that lived in shared/api, where
/// NewPoll had drifted to 51 — the server's slot for DeletedPublicationComment —
/// and the wire it was written against, where the hub sent the event as its
/// number while the list sent its name.
/// </remarks>
public class NotificationVocabularyShould
{
    private const string SharedType =
        "DM.Workers.NotificationDispatcher.Implementation.NotificationText";

    /// <summary>
    /// EventType.Unknown: an event with no title of its own. Spelled by value
    /// because the enum is reached through the signature of the method it keys,
    /// which keeps this suite off a reference to the domain.
    /// </summary>
    private const int UntitledEvent = 0;

    private static readonly Assembly Dispatcher =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly;

    private static readonly string ClientSource =
        Path.Combine("src", "DM.Web.Client", "src");

    /// <summary>The slice that owns the client half of the table.</summary>
    private static readonly string NotificationSlice =
        Path.Combine(ClientSource, "entities", "notification");

    private static readonly string ClientTitleTable =
        Path.Combine(NotificationSlice, "lib", "notificationTitle.ts");

    /// <summary>The one place the client is allowed to spell the server's events.</summary>
    private static readonly string ClientEventTable =
        Path.Combine(ClientSource, "shared", "api", "models", "notifications", "index.ts");

    /// <summary>
    /// How many of an enum's members have to be server events before it counts as
    /// a copy of the vocabulary rather than a coincidence of naming.
    /// </summary>
    private const int MembersThatMakeACopy = 3;

    /// <summary>One row of the client table: [NotificationType.X]: "Y".</summary>
    private static readonly Regex ClientRow = new(
        @"\[NotificationType\.(\w+)\]:\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>The word the client falls back to.</summary>
    private static readonly Regex ClientFallback = new(
        @"UNKNOWN_TITLE\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>
    /// A word given to an event: a table row [NotificationType.X]: "..." or a
    /// switch arm that answers one with a literal. Both are shapes the drift
    /// actually took. Naming an event to decide what to do about it is not one of
    /// them, and a screen reacting to a push does exactly that.
    /// </summary>
    private static readonly Regex TitlesAnEvent = new(
        @"NotificationType\.\w+\s*\]?\s*:\s*(return\s+)?""", RegexOptions.Compiled);

    /// <summary>Any TypeScript enum: its name and its body.</summary>
    private static readonly Regex TypeScriptEnum = new(
        @"enum\s+(\w+)\s*\{([^}]*)\}", RegexOptions.Compiled);

    /// <summary>One member of an enum body: the name and the value written for it.</summary>
    private static readonly Regex EnumMember = new(
        @"^\s*(\w+)\s*=\s*([^,\r\n]+?),?\s*$", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A line comment, dropped so a commented-out member is not read as one.</summary>
    private static readonly Regex LineComment = new(@"//[^\r\n]*", RegexOptions.Compiled);

    [Fact]
    public void TitleAnEventTheSameWayInEveryChannel()
    {
        var table = File.ReadAllText(Path.Combine(RepositoryRoot, ClientTitleTable));
        var rows = ClientRow.Matches(table);
        rows.Count.Should().BeGreaterThan(10,
            "a rule that matches nothing passes: the list titles more than ten events");

        var getTitle = Titles;
        var events = ServerEvents;

        var drift = new List<string>();
        foreach (Match row in rows)
        {
            var name = row.Groups[1].Value;
            var shown = row.Groups[2].Value;

            if (!Enum.TryParse(events, name, false, out var answered))
            {
                drift.Add($"{name}: the dispatcher has no such event");
                continue;
            }

            var sent = (string)getTitle.Invoke(null, new[] { answered })!;
            if (sent != shown)
            {
                drift.Add($"{name}: \"{shown}\" in the list, \"{sent}\" in the letter");
            }
        }

        var fallback = ClientFallback.Match(table);
        fallback.Success.Should().BeTrue(
            "the list needs a word for an event it has no title for");

        var untitled = (string)getTitle.Invoke(
            null, new[] { Enum.ToObject(events, UntitledEvent) })!;
        if (fallback.Groups[1].Value != untitled)
        {
            drift.Add(
                $"an untitled event: \"{fallback.Groups[1].Value}\" in the list, " +
                $"\"{untitled}\" in the letter");
        }

        drift.Should().BeEmpty(
            "a second word for one event is not a synonym: the list and the letter " +
            "reach the same person minutes apart, and he is given both");
    }

    [Fact]
    public void TitleAnEventOnlyInTheNotificationSlice()
    {
        var root = RepositoryRoot;
        var slice = Path.Combine(root, NotificationSlice);

        var titling = SourceFiles(Path.Combine(root, ClientSource))
            .Where(file => TitlesAnEvent.IsMatch(File.ReadAllText(file)))
            .ToArray();

        titling
            .Where(file => file.StartsWith(slice, StringComparison.Ordinal))
            .Should().NotBeEmpty(
                "a rule that matches nothing passes: the slice keys its titles by the enum");

        titling
            .Where(file => !file.StartsWith(slice, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Should().BeEmpty(
                "a screen that puts a word next to an event sooner or later puts its " +
                "own word there; the words are imported from the notification entity " +
                "instead");
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
            if (!Enum.TryParse(ServerEvents, name, false, out _))
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
                    .Count(member => Enum.TryParse(ServerEvents, member.Name, false, out _));
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

    /// <summary>The dispatcher's title table, reached by reflection.</summary>
    private static MethodInfo Titles => Dispatcher.GetType(SharedType, throwOnError: true)!
        .GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// The server's event vocabulary, taken from the signature of the method it
    /// keys rather than from a reference to the domain.
    /// </summary>
    private static Type ServerEvents => Titles.GetParameters()[0].ParameterType;

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
    private static IEnumerable<string> SourceFiles(string directory)
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
