using System;
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
/// A stream that gains a row per action and loses none grows without bound, so every table either declares
/// a term in the retention registry or is named here as bounded state.
/// </remarks>
public class RetentionShould
{
    /// <summary>
    /// State rather than stream: each row is bounded by the entity it belongs to and goes with it.
    /// </summary>
    private static readonly string[] Bounded =
    [
        "AchievementCategories", "AchievementTypes", "AwardTypes",
        "Bans", "Blogs", "BlogAssistants", "BlogBlacklists", "Boards", "BoardModerators",
        "Chats", "Characters", "CharacterAttributes", "Comments",
        "CommentEdits", "ContestSeries", "DiceRolls", "FundraisingGoals", "Games",
        "GameAssistants", "GameBlacklists", "GameReviews", "GameTags", "GlobalChatEvents",
        "GlobalChatEventParticipants", "Likes", "Messages", "MessageEdits",
        // Bounded by its game: one row per game, taken by the cascade the day a
        // game is deleted for real. A term over it would take the sheet a live
        // game describes its characters by.
        "GameAttributeSchemas",
        // Bounded by the catalogue it is: the starting sheets the site offers,
        // hidden by a moderator rather than aged out. A term over it would take
        // the sheet the creation form fills itself from.
        "AttributeSchemaTemplates",
        "ModeratedProfileNotes", "NotepadEntries",
        // Bounded by its notification: the CASCADE from Notifications is what
        // collects it when the retention term takes the parent row.
        "NotificationRecipients",
        "PendingRegistrations", "PeriodDigestTopics", "Polls", "PollOptions", "PollVotes",
        "Posts", "PostEdits", "PostPendencies", "PostReviews", "Publications",
        "RoomAccesses", "Rooms", "Rubrics", "Subscriptions", "Tags",
        "TagGroups", "Tickets", "Tokens", "Topics", "TopicEdits",
        // Bounded by its ticket: a hundred comments at most, taken by the cascade
        // with the ticket.
        "TicketComments",
        "Uploads", "Users", "UserAchievements", "UserAwards", "UserBlacklists",
        "UserChatLinks", "UserContacts", "UserEndorsements", "UserLoginRecords",
        "UserProfileNotes",
        // Bounded by its expiry: the hourly session purge deletes by
        // ExpirationUtc, a lifetime the session carries rather than a term the
        // registry would impose on top of it.
        "UserSessions",
        "UserSettings", "UsernameChangeRequests", "UsernameHistories", "Warnings",
        "WebsiteTestimonials",
        // The second factor and its codes go with the account; an abandoned setup has a pass of its own.
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
        tables.Should().NotBeEmpty(
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
    /// The terms were approved as they stand; a silent edit is a retention decision nobody made.
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
        policies.Should().HaveCount(6, "a policy added or dropped is a retention decision too");
    }
}
