using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using DM.Domain.Account.Configuration;
using DM.Infrastructure.Persistence.RelationalStorage;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests;

/// <summary>
/// Every table says how long it keeps what it is given.
/// </summary>
/// <remarks>
/// The successor of MongoRetentionShould, over the sweep registry instead of
/// TTL index declarations. A stream that gains a row per user action and loses
/// none grows without any bound, and the silence reads the same whether the
/// term was weighed and rejected or never thought about. So a table either
/// declares a term in the retention registry or is named here as state bounded
/// by what it belongs to — for every table of the context, with no third state
/// (INV-5).
/// </remarks>
public class RetentionShould
{
    /// <summary>
    /// State rather than stream: one row per user, per game, per session, per
    /// vote, per roll of a post. Each is bounded by the entity it belongs to and
    /// goes when that goes, so a term here would only delete something still in
    /// use. Adding a table to this list is a decision, which is why it is a list
    /// and not a heuristic.
    /// </summary>
    private static readonly string[] Bounded =
    [
        "AchievementCategories", "AchievementTypes", "AttributeSchemata", "AwardTypes",
        "Bans", "Blogs", "BlogAssistants", "BlogBlacklists", "Boards", "BoardModerators",
        "Chats", "Characters", "CharacterAttributes", "CharacterEdits", "Comments",
        "CommentEdits", "ContestSeries", "DiceRolls", "FundraisingGoals", "Games",
        "GameAssistants", "GameBlacklists", "GameReviews", "GameTags", "GlobalChatEvents",
        "GlobalChatEventParticipants", "Likes", "Messages", "MessageEdits",
        "ModeratedProfileNotes", "NotepadEntries",
        // Bounded by its notification: the CASCADE from Notifications is what
        // collects it when the retention term takes the parent row.
        "NotificationRecipients",
        "PendingRegistrations", "PeriodDigestTopics", "Polls", "PollOptions", "PollVotes",
        "Posts", "PostEdits", "PostPendencies", "PostReviews", "Publications",
        "RoomAccesses", "Rooms", "Rubrics", "RubricAccesses", "Subscriptions", "Tags",
        "TagGroups", "Tickets", "TicketResponses", "Tokens", "Topics", "TopicEdits",
        "Uploads", "Users", "UserAchievements", "UserAwards", "UserBlacklists",
        "UserChatLinks", "UserContacts", "UserEndorsements", "UserLoginRecords",
        "UserProfileNotes",
        // Bounded by its expiry: the hourly session purge deletes by
        // ExpirationUtc, a lifetime the session carries rather than a term the
        // registry would impose on top of it.
        "UserSessions",
        "UserSettings", "UsernameChangeRequests", "UsernameHistories", "Warnings",
        "WebsiteTestimonials",
        // The second factor and its recovery codes: one row and a fixed set of
        // rows per account, bounded by the account and taken by the cascade when
        // it goes. A term over either of them would delete a credential still in
        // use. The setup abandoned before confirmation is cleared by a pass of
        // its own, which is not retention over a stream but the removal of a
        // credential nobody finished issuing.
        "UserTwoFactors", "UserTwoFactorRecoveryCodes",
    ];

    private static string[] RegistryTables() => RetentionRegistry
        .Policies(new AuthenticationConfiguration())
        .Select(policy => policy.Table)
        .ToArray();

    private static string[] ContextTables() => typeof(DmDbContext).Assembly
        .GetTypes()
        .Select(t => t.GetCustomAttribute<TableAttribute>()?.Name)
        .Where(name => name != null)
        .Select(name => name!)
        .ToArray();

    [Fact]
    public void DeclareATermOrABoundForEveryTable()
    {
        var tables = ContextTables();
        tables.Should().HaveCountGreaterThan(50,
            "a rule that matches nothing passes: every entity of the context carries [Table]");

        tables
            .Where(table => !Bounded.Contains(table, StringComparer.Ordinal))
            .Where(table => !RegistryTables().Contains(table, StringComparer.Ordinal))
            .Should().BeEmpty(
                "a table that gains a row per user action and loses none grows without " +
                "any bound, and nothing distinguishes that from a decision to keep it all");
    }

    [Fact]
    public void KeepEveryPolicyOverATableThatExists() =>
        RegistryTables().Except(ContextTables(), StringComparer.Ordinal)
            .Should().BeEmpty(
                "a policy over a table the context no longer declares deletes nothing and " +
                "outlives its reason");

    [Fact]
    public void KeepTheListOfBoundedTablesHonest() =>
        Bounded.Except(ContextTables(), StringComparer.Ordinal)
            .Should().BeEmpty(
                "an exemption for a table the context no longer declares guards nothing " +
                "and outlives its reason");

    [Fact]
    public void NameNoTableTwice() =>
        Bounded.Intersect(RegistryTables(), StringComparer.Ordinal)
            .Should().BeEmpty(
                "a table cannot be both a stream with a term and bounded state; one of the " +
                "two declarations is a leftover");

    /// <summary>
    /// The first four terms carry over from the TTL era unchanged, and the
    /// outbox term was approved with its design (W1.5); a silent edit here is a
    /// data-retention decision nobody made.
    /// </summary>
    [Fact]
    public void KeepTheTermsThePoliciesWereApprovedWith()
    {
        var policies = RetentionRegistry
            .Policies(new AuthenticationConfiguration())
            .ToDictionary(policy => policy.Table);

        policies["LoginAttempts"].Term.Should().Be(TimeSpan.FromHours(24),
            "the default lockout window, read from AuthenticationConfiguration rather than mirrored");
        policies["SecurityAuditEntries"].Term.Should().Be(TimeSpan.FromDays(180));
        policies["Notifications"].Term.Should().Be(TimeSpan.FromDays(180));
        policies["UnreadCounters"].Term.Should().Be(TimeSpan.FromDays(7));
        // Published rows only: an unpublished row carries a NULL stamp that
        // never matches the cutoff, so the sweep cannot touch a backlog.
        policies["OutboxEvents"].Term.Should().Be(TimeSpan.FromDays(7),
            "a week of published rows covers investigating a delivery incident");
        // The challenge lives five minutes; the day is slack for a row the end
        // of a login failed to delete, and not a second lifetime of the challenge.
        policies["TwoFactorChallenges"].Term.Should().Be(TimeSpan.FromDays(1));
        policies.Should().HaveCount(6,
            "four policies replaced the four dead TTL indexes, the fifth came with the " +
            "outbox and the sixth with the second factor");
    }
}
