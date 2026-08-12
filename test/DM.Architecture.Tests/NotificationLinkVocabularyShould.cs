using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One notification leads to one place, in the list and in the letter about it.
/// </summary>
/// <remarks>
/// The sibling rule holds the two halves of the title table together, and this one
/// holds the two halves of the link table the same way and for the same reason: the
/// list and the letter reach one person minutes apart, and a destination that
/// differs between them is a reader sent to two places for one event. Neither tier
/// can import the other, so the pair is held from outside — the client table is read
/// from its source and put to the server's own GetUrl, identifier and all.
///
/// The server table is allowed to know more, and does: the list links what a screen
/// links, while the letter goes out for a frozen game, an invitation and an owed
/// post as well. What it is not allowed to do is answer differently.
///
/// The exemption list is the other direction of the same rule. Seven of the events
/// the client table names are ones no notification ever carries — a generator
/// answers the event and renames its notification to another, or nothing answers it
/// at all — so a row for one of them on the server would be a destination that can
/// never be reached, and the honesty of the table is that it holds none.
/// </remarks>
public class NotificationLinkVocabularyShould
{
    private const string LinkType =
        "DM.Workers.NotificationDispatcher.Implementation.NotificationLink";

    /// <summary>The client half of the table.</summary>
    private static readonly string ClientLinkTable = Path.Combine(
        "src", "DM.Web.Client", "src", "entities", "notification", "lib", "notificationLink.ts");

    /// <summary>An address no deployment of this site answers on.</summary>
    private const string PublicUrl = "https://example.test";

    /// <summary>Stands in for whatever identifier a generator would have written.</summary>
    private const string Identifier = "identifier";

    /// <summary>
    /// One arm of the client table: the events it answers for, the payload field it
    /// reads and the path it builds out of that field.
    /// </summary>
    private static readonly Regex ClientRow = new(
        @"(?<cases>(?:\s*case NotificationType\.\w+:)+)\s*return payload\.(?<field>\w+)\s*\?\s*`(?<path>[^`]+)`\s*:\s*null;",
        RegexOptions.Compiled);

    /// <summary>One event named in an arm.</summary>
    private static readonly Regex CaseName = new(
        @"NotificationType\.(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// Events the list links and no notification ever carries.
    /// </summary>
    /// <remarks>
    /// NewGame, NewTopic and NewTopicComment wake a subscription generator which
    /// renames its notification on the way out, so the event that arrives is
    /// NewGameFromSubscribedAuthor, NewTopicFromSubscribedAuthor or
    /// NewCommentInSubscribedTopic and never the trigger. NewTopicInSubscribedBoard,
    /// NewPostInSubscribedGame and the two answered blog invitations are named by no
    /// generator at all. The server table carries no row for any of them, and this
    /// list is what makes that a decision rather than an omission.
    /// </remarks>
    private static readonly HashSet<string> NeverCarried = new(StringComparer.Ordinal)
    {
        "NewGame",
        "NewTopic",
        "NewTopicComment",
        "NewTopicInSubscribedBoard",
        "NewPostInSubscribedGame",
        "BlogInvitationAccepted",
        "BlogInvitationRejected"
    };

    /// <summary>
    /// Reached by name, because the table is internal to the worker and this suite is
    /// not on its InternalsVisibleTo list.
    /// </summary>
    private static readonly MethodInfo Links =
        typeof(DM.Workers.NotificationDispatcher.Startup).Assembly
            .GetType(LinkType, throwOnError: true)!
            .GetMethod("GetUrl", BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void SendAReaderWhereTheListWouldSendHim()
    {
        var rows = ClientRow.Matches(File.ReadAllText(Path.Combine(RepositoryRoot, ClientLinkTable)));
        rows.Count.Should().BeGreaterThan(5,
            "a rule that matches nothing passes: the list answers for more than five " +
            "groups of events");

        var addresses = new SiteAddressConfiguration { PublicUrl = PublicUrl };
        var drift = new List<string>();
        var paired = 0;

        foreach (Match row in rows)
        {
            var field = row.Groups["field"].Value;
            var payload = new Dictionary<string, string> { [Capitalized(field)] = Identifier };
            var expected = PublicUrl + row.Groups["path"].Value
                .Replace($"${{payload.{field}}}", Identifier, StringComparison.Ordinal);

            foreach (Match named in CaseName.Matches(row.Groups["cases"].Value))
            {
                var spelling = named.Groups[1].Value;
                if (!Enum.TryParse<EventType>(spelling, false, out var carried))
                {
                    drift.Add($"{spelling}: the dispatcher has no event of that name");
                    continue;
                }

                var built = Url(carried, payload, addresses);
                if (NeverCarried.Contains(spelling))
                {
                    if (built != null)
                    {
                        drift.Add(
                            $"{spelling}: {built} in the letter, and no notification " +
                            "ever carries the event");
                    }

                    continue;
                }

                paired++;
                if (built != expected)
                {
                    drift.Add(
                        $"{spelling}: {expected} in the list, {built ?? "nothing"} in the letter");
                }
            }
        }

        paired.Should().BeGreaterThan(9,
            "a rule that matches nothing passes: a dozen of the events the list links " +
            "are events a letter goes out for, and an emptied server table would pair " +
            "with none of them");

        drift.Should().BeEmpty(
            "the list and the letter reach one person minutes apart, and a destination " +
            "that differs between them sends him to two places for one event");
    }

    /// <summary>
    /// An exemption outlives its reason the moment the row it excuses is gone.
    /// </summary>
    [Fact]
    public void KeepTheListOfEventsNobodyCarriesHonest()
    {
        var named = CaseName
            .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, ClientLinkTable)))
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        named.Should().NotBeEmpty("a rule that matches nothing passes: the list names events");

        NeverCarried.Except(named).Should().BeEmpty(
            "an exemption for an event the list no longer links excuses nothing, and the " +
            "reason it was written down cannot be read off anything");
    }

    /// <summary>The server's answer, asked the way a channel asks it.</summary>
    private static string? Url(
        EventType eventType, object payload, SiteAddressConfiguration addresses) =>
        (string?)Links.Invoke(null, new object[] { eventType, payload, addresses });

    /// <summary>The payload field as the server spells it: blogId is BlogId.</summary>
    private static string Capitalized(string field) =>
        char.ToUpperInvariant(field[0]) + field[1..];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
