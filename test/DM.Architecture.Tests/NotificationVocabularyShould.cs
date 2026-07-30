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
/// client table is read from its source and put to GetTitle. The second test is
/// what makes the first one worth having — it keeps the client table single by
/// keeping the enum it is keyed by inside the notification slice, so there is
/// nowhere else for the words to appear.
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

    /// <summary>One row of the client table: [NotificationType.X]: "Y".</summary>
    private static readonly Regex ClientRow = new(
        @"\[NotificationType\.(\w+)\]:\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>The word the client falls back to.</summary>
    private static readonly Regex ClientFallback = new(
        @"UNKNOWN_TITLE\s*=\s*""([^""]+)""", RegexOptions.Compiled);

    /// <summary>Any qualified use of the enum the titles are keyed by.</summary>
    private static readonly Regex NamesAnEvent = new(
        @"(?<![A-Za-z])NotificationType\.\w+", RegexOptions.Compiled);

    [Fact]
    public void TitleAnEventTheSameWayInEveryChannel()
    {
        var table = File.ReadAllText(Path.Combine(RepositoryRoot, ClientTitleTable));
        var rows = ClientRow.Matches(table);
        rows.Count.Should().BeGreaterThan(10,
            "a rule that matches nothing passes: the list titles more than ten events");

        var getTitle = Dispatcher.GetType(SharedType, throwOnError: true)!
            .GetMethod("GetTitle", BindingFlags.Public | BindingFlags.Static)!;
        var events = getTitle.GetParameters()[0].ParameterType;

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
    public void NameAnEventOnlyInTheNotificationSlice()
    {
        var root = RepositoryRoot;
        var slice = Path.Combine(root, NotificationSlice);

        var naming = SourceFiles(Path.Combine(root, ClientSource))
            .Where(file => NamesAnEvent.IsMatch(File.ReadAllText(file)))
            .ToArray();

        naming
            .Where(file => file.StartsWith(slice, StringComparison.Ordinal))
            .Should().NotBeEmpty(
                "a rule that matches nothing passes: the slice keys its titles by the enum");

        naming
            .Where(file => !file.StartsWith(slice, StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(root, file).Replace('\\', '/'))
            .Should().BeEmpty(
                "a screen that names an event sooner or later names it in words of " +
                "its own; the words are imported from the notification entity instead");
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
