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
/// One notification leads to one place, in the list and in the letter about it,
/// and the client builds that place for none.
/// </summary>
/// <remarks>
/// The list used to build its links from a table of its own, paired from here with
/// the server's table of paths, identifier and all. The pairing held the two halves
/// to the same destination, and it could not make the client half complete: the
/// letter went out for a frozen game, an invitation and an owed post, and the list
/// showed the same notification as a line nobody could click, because nobody had
/// remembered the client when the server learned the event.
///
/// So the list row arrives with its path: the server takes it from the table the
/// letter's link is built from (NotificationDestination), and the letter puts the
/// root of the site in front of the same path, which NotificationLinkShould in
/// DM.Infrastructure.Messaging.Tests holds. What is left to hold here is the
/// boundary: no client source builds a path for an event, in the shapes the client
/// table took (a switch arm that answers an event with a path, a row keyed by the
/// enum) or under another name (a row keyed by the server's event name).
/// </remarks>
public class NotificationLinkVocabularyShould
{
    private static readonly string ClientSource =
        Path.Combine("src", "DM.Web.Client", "src");

    /// <summary>
    /// A switch arm, one event or several, that answers with a path: case
    /// NotificationType.X: return payload.id ? `/game/${payload.id}` : null.
    /// </summary>
    private static readonly Regex PathArm = new(
        @"case\s+NotificationType\.\w+\s*:(?:\s*case\s+NotificationType\.\w+\s*:)*\s*return\b[^;]*?[""'`]/",
        RegexOptions.Compiled);

    /// <summary>A row keyed by the enum whose value holds a path, on one line.</summary>
    private static readonly Regex PathRow = new(
        @"\[NotificationType\.\w+\]\s*:[^\r\n]*?[""'`]/", RegexOptions.Compiled);

    /// <summary>
    /// A row keyed by a bare name whose value holds a path, on one line; it counts
    /// when the name is one of the server's events.
    /// </summary>
    private static readonly Regex NamedPathRow = new(
        @"^\s*[""']?(\w+)[""']?\s*:[^\r\n]*?[""'`]/", RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>A line comment, dropped so a commented-out row is not read as one.</summary>
    private static readonly Regex LineComment = new(@"//[^\r\n]*", RegexOptions.Compiled);

    /// <summary>
    /// The shapes the client table was written in, kept here so the patterns above
    /// are shown to find them: a rule that matches nothing passes, and the table they
    /// were written against is gone.
    /// </summary>
    private const string FormerLinkTable = """
        switch (notification.eventType) {
          case NotificationType.NewPublication:
          case NotificationType.LikedPublication:
            return payload.publicationId
              ? `/publication/${payload.publicationId}`
              : null;
          case NotificationType.BanIssued:
            return payload.username ? `/users/${payload.username}` : null;
        }
        const PATHS = {
          [NotificationType.NewGame]: (payload) => `/game/${payload.gameId}`,
        };
        const RENAMED = {
          NewTopic: (payload) => "/forum-topic/" + payload.topicId,
        };
        """;

    /// <summary>What a screen reacting to a push writes, which is no path.</summary>
    private const string PushReaction = """
        switch (notification.eventType) {
          case NotificationType.NewMessage:
            void refresh();
            break;
        }
        const route = { name: "account", path: "/account" };
        """;

    [Fact]
    public void KnowTheShapesOfALinkTable()
    {
        PathArm.Matches(FormerLinkTable).Should().HaveCount(2,
            "both arms of the former table answer an event with a path");
        PathRow.Matches(FormerLinkTable).Should().HaveCount(1,
            "a row keyed by the enum is the other shape of the same table");
        NamedRows(FormerLinkTable).Should().BeEquivalentTo(new[] { "NewTopic" },
            "a row keyed by the server's name is the same table under another name");

        PathArm.IsMatch(PushReaction).Should().BeFalse(
            "naming an event to react to it builds no path");
        PathRow.IsMatch(PushReaction).Should().BeFalse(
            "naming an event to react to it builds no path");
        NamedRows(PushReaction).Should().BeEmpty(
            "a route of the application is not a notification's destination");
    }

    [Fact]
    public void BuildNoPathForAnEventOnTheClient()
    {
        var root = RepositoryRoot;
        var files = NotificationVocabularyShould
            .SourceFiles(Path.Combine(root, ClientSource))
            .ToArray();
        files.Should().HaveCountGreaterThan(100,
            "a rule that reads nothing passes: the client has hundreds of sources");

        var building = new List<string>();
        foreach (var file in files)
        {
            var source = LineComment.Replace(File.ReadAllText(file), string.Empty);
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (PathArm.IsMatch(source) || PathRow.IsMatch(source))
            {
                building.Add($"{relative}: a path built for NotificationType");
            }

            var named = NamedRows(source);
            if (named.Count > 0)
            {
                building.Add($"{relative}: paths for {string.Join(", ", named)}");
            }
        }

        building.Should().BeEmpty(
            "the list row arrives with the path the letter's link is built from; a " +
            "path for an event on the client is a second table, and the second table " +
            "is how a notification the letter linked became a line nobody could click");
    }

    /// <summary>The server's event names a source keys a path by.</summary>
    private static List<string> NamedRows(string source) =>
        NamedPathRow.Matches(LineComment.Replace(source, string.Empty))
            .Select(row => row.Groups[1].Value)
            .Where(name => name != nameof(EventType.Unknown)
                && Enum.TryParse<EventType>(name, false, out _))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
