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

/// <summary>
/// One notification leads to one place, in the list and in the letter about it,
/// and the client builds that place for none.
/// </summary>
/// <remarks>
/// The list row arrives with its path from NotificationDestination, the table the letter's link is
/// built from. What is held here is the boundary: no client source builds a path for an event.
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

    [Fact]
    public void BuildNoPathForAnEventOnTheClient()
    {
        var root = RepositoryLayout.Root;
        var files = NotificationVocabularyShould
            .SourceFiles(Path.Combine(root, ClientSource))
            .ToArray();
        files.Should().NotBeEmpty(
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
}
