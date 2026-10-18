using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// What a removed account wrote is not lost by a read, and every read that may lose a
/// row for a removed account says so by name.
/// </summary>
/// <remarks>
/// The account row sits behind the soft-delete filter, and a required navigation to a
/// filtered row is an INNER JOIN onto the filtered users: the row on the other side of
/// the join goes with the account. That is how the games of removed masters, their
/// posts, topics and comments fell out of every list while each read looked right on
/// its own. The content reads now lift the account filter (RemovedAuthors in the
/// persistence project) and nest a removed account as nobody.
///
/// The rule is read off the statement, the one place the join is visible: every
/// projection entry point of the persistence layer is compiled to SQL against the real
/// model, and an INNER JOIN onto the filtered users subquery is what losing a row looks
/// like. A projection that reads what people wrote must have none; a projection that
/// may have one is named below with the reason it is allowed to. A new projection that
/// joins users that way fails here until somebody decides which of the two it is - the
/// day a new kind of content is added, the question is asked before the first removed
/// author makes its rows disappear, not after.
/// </remarks>
public class RemovedAuthorRowsShould
{
    /// <summary>
    /// The reads of what people wrote. None of them may lose a row because its author,
    /// its master or its editor was removed.
    /// </summary>
    private static readonly string[] KeepEveryRow =
    [
        "ForumMappers.ProjectToTopic",
        "ForumMappers.ProjectToTopicCommentToDelete",
        "CommentProjections.ProjectToComment",
        "GameMappers.ProjectToGame",
        "GameMappers.ProjectToGameDetails",
        "GameMappers.ProjectToRoom",
        "GameMappers.ProjectToRoomToUpdate",
        "GameMappers.ProjectToPostPendency",
        "GameMappers.ProjectToCharacter",
        "GameMappers.ProjectToRoomAccess",
        "PostMappers.ProjectToPost",
        "PostMappers.ProjectToGameCommentToDelete",
        "BlogMappers.ProjectToBlog",
        "BlogMappers.ProjectToPublication",
        "BlogMappers.ProjectToBlogCommentToDelete",
        "BlogMappers.ProjectToPublicationCommentToDelete",
        "ReviewMappers.ProjectToGameReview",
        "ReviewMappers.ProjectToPostReview",
        "MessagingMappers.ProjectToMessage",
        "ModerationMapper.ProjectToTicket",
        "ModerationMapper.ProjectToTicketComment",
    ];

    /// <summary>
    /// The reads allowed to lose a row for a removed account, and why. Adding here is a
    /// decision about a kind of row, not a way to make the test pass.
    /// </summary>
    private static readonly Dictionary<string, string> MayLoseARow = new()
    {
        ["ForumMappers.ProjectToBoard"] =
            "the row lost is a moderator in the board's roster, and a removed account moderates nothing",
        ["MessagingMappers.ProjectToChat"] =
            "the rows lost are participants of the chat and edit marks of its last message, never the chat",
        ["MessagingMappers.ProjectToGlobalChatEvent"] =
            "an event is the organiser's announcement, and the participants lost are a roster",
        ["MessagingMappers.ProjectToGlobalChatEventParticipant"] =
            "a participant is a person in a roster, and a removed account is nobody",
        ["AchievementMapper.ProjectToUserAchievement"] =
            "an achievement belongs to the account that earned it and is shown only on its profile",
        ["AwardMapper.ProjectToUserAward"] =
            "an award belongs to the account that received it and is shown only on its profile",
        ["UserEndorsementMapper.ProjectToUserEndorsement"] =
            "an endorsement is one account's word about another, and it goes with either of them",
        ["WebsiteTestimonialMapper.ProjectToWebsiteTestimonial"] =
            "a testimonial is an account's word about the site, and it goes with the account",
        ["ModerationMapper.ProjectToWarning"] =
            "a moderation record names both sides, and the moderation screens read it for a live account",
        ["ModerationMapper.ProjectToBan"] =
            "a moderation record names both sides, and the moderation screens read it for a live account",
        ["ModerationMapper.ProjectToModeratedProfileNote"] =
            "a note about an account is read on that account's moderated profile",
        ["PersonalMapper.ProjectToBlacklistEntry"] =
            "a blacklist names people, and a removed account is nobody",
        ["PersonalMapper.ProjectToUserProfileNote"] =
            "a personal note is about a person, and a removed account is nobody",
    };

    /// <summary>
    /// An INNER JOIN onto the users table under its soft-delete filter: the shape a
    /// required navigation to an account takes when the filter is in force.
    /// </summary>
    private static readonly Regex LosesARow = new(
        @"INNER JOIN \(\s*SELECT [^()]*?FROM ""Users"" AS (\w+)\s+WHERE NOT \(\1\.""IsRemoved""\)\s*\) AS",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static DmDbContext Probe() => new(new DbContextOptionsBuilder<DmDbContext>()
        // Statement text only: the connection is never opened.
        .UseNpgsql("Host=localhost;Database=removed-author-probe;Username=probe;Password=probe")
        .Options);

    /// <summary>
    /// Every projection entry point of the persistence layer: a static ProjectTo*
    /// method over the query of one entity.
    /// </summary>
    private static IReadOnlyDictionary<string, MethodInfo> Projections() =>
        typeof(DmDbContext).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.Name.StartsWith("ProjectTo", StringComparison.Ordinal))
            .Where(method => method.GetParameters() is [{ ParameterType.IsGenericType: true } source] &&
                             source.ParameterType.GetGenericTypeDefinition() == typeof(IQueryable<>))
            .ToDictionary(method => $"{method.DeclaringType!.Name}.{method.Name}");

    /// <summary>The statement one projection compiles to over the whole set.</summary>
    private static string Statement(DmDbContext probe, MethodInfo projection)
    {
        var entity = projection.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        var set = typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!
            .MakeGenericMethod(entity)
            .Invoke(probe, null)!;
        var query = (IQueryable)projection.Invoke(null, [set])!;
        return query.ToQueryString();
    }

    [Fact]
    public void KeepEveryRowAContentReadProjects()
    {
        using var probe = Probe();
        var projections = Projections();

        var losing = KeepEveryRow
            .Where(name => LosesARow.IsMatch(Statement(probe, projections[name])))
            .ToArray();

        losing.Should().BeEmpty(
            "a read of what people wrote keeps the row whose author, master or editor was " +
            "removed, and nests that account as nobody; these join the filtered users instead");
    }

    [Fact]
    public void NameEveryReadThatMayLoseARow()
    {
        using var probe = Probe();

        var unnamed = Projections()
            .Where(projection => LosesARow.IsMatch(Statement(probe, projection.Value)))
            .Select(projection => projection.Key)
            .Where(name => !MayLoseARow.ContainsKey(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        unnamed.Should().BeEmpty(
            "each of these drops a row when an account it joins is removed; if the row is " +
            "something a person wrote, lift the account filter the way the content reads do " +
            "(RemovedAuthors), otherwise name it in MayLoseARow with the reason");
    }

    /// <summary>
    /// Both lists keep naming projections that exist and still behave as listed, or a
    /// rename leaves a hole and a fixed read leaves a stale exemption.
    /// </summary>
    [Fact]
    public void KeepBothListsHonest()
    {
        using var probe = Probe();
        var projections = Projections();

        KeepEveryRow.Concat(MayLoseARow.Keys)
            .Where(name => !projections.ContainsKey(name))
            .Should().BeEmpty("a listed projection that no longer exists guards nothing");

        MayLoseARow.Keys
            .Where(name => projections.ContainsKey(name))
            .Where(name => !LosesARow.IsMatch(Statement(probe, projections[name])))
            .Should().BeEmpty("an exemption for a read that keeps its rows after all outlives its reason");
    }
}
