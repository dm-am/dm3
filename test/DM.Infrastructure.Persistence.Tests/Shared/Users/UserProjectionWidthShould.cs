using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DM.Infrastructure.Persistence.Repositories.Game;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbPost = DM.Infrastructure.Persistence.Entities.Game.Posts.Post;

namespace DM.Infrastructure.Persistence.Tests.Shared.Users;

/// <summary>
/// A nested user costs the columns the formula names, not the users row.
/// </summary>
/// <remarks>
/// GeneralUserProjections is a stored expression spliced into every list
/// projection. As an opaque method the same formula forced EF to materialise
/// the whole users row per nested author - password hash, salt, pending
/// email, bio - on some forty list paths. The statement is what states the
/// width, so the test reads the statement: every users column the SQL
/// references must be one the formula (or a join, or the soft-delete filter)
/// actually needs, for the two hottest representative paths - the posts of a
/// room and the games catalog.
/// </remarks>
public class UserProjectionWidthShould
{
    private static DmDbContext Probe() => new(new DbContextOptionsBuilder<DmDbContext>()
        // Statement text only: the connection is never opened.
        .UseNpgsql("Host=localhost;Database=projection-width-probe;Username=probe;Password=probe")
        .Options);

    /// <summary>
    /// The users columns the shared formula reads, plus the members a query
    /// legitimately touches around it: the join keys (UserId,
    /// AvatarUploadId), the soft-delete filter (IsRemoved) and the computed
    /// newbie flag the assistant stubs read (IsNewbie). Nothing else of the
    /// users row may appear in any list statement.
    /// </summary>
    private static readonly HashSet<string> AllowedUserColumns =
    [
        "UserId", "Username", "Email", "Role", "AccessPolicy", "LastActivityUtc",
        "Status", "Name", "Location", "Gender", "BirthdayDate", "ShowBirthday",
        "RatingDisabled", "QualityRating", "QuantityRating", "CreatedUtc",
        "IsUnderModerationWatch", "AvatarUploadId", "IsRemoved", "IsNewbie"
    ];

    /// <summary>
    /// Every column the statement reads off any alias of the Users table.
    /// </summary>
    private static IReadOnlyCollection<string> ReferencedUserColumns(string sql)
    {
        var aliases = Regex.Matches(sql, @"(?:FROM|JOIN)\s+""Users""\s+AS\s+(\w+)")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToList();
        aliases.Should().NotBeEmpty("the projection nests users, so the statement joins the table");

        return aliases
            .SelectMany(alias => Regex.Matches(sql, $@"\b{Regex.Escape(alias)}\.""(\w+)""")
                .Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();
    }

    private static void AssertNarrow(string sql)
    {
        sql.Should().NotContain("PasswordHash");
        sql.Should().NotContain("\"Salt\"");
        sql.Should().NotContain("PendingEmail");

        // The second factor lives in tables of its own precisely so that it can
        // never ride along in a projection of a user, and a list read has no
        // reason to name them. Asserted rather than assumed: the day somebody
        // adds "does this author have a factor" to a nested user, the secret
        // starts travelling with every page of a room.
        sql.Should().NotContain("UserTwoFactors");
        sql.Should().NotContain("UserTwoFactorRecoveryCodes");
        sql.Should().NotContain("Secret");

        var referenced = ReferencedUserColumns(sql);
        referenced.Should().OnlyContain(
            column => AllowedUserColumns.Contains(column),
            "a users column outside the formula means the projection stopped inlining");
        referenced.Should().Contain(
            ["Username", "QuantityRating", "AvatarUploadId"],
            "the formula's own columns are what the narrow join is for");
    }

    [Fact]
    public void FetchOnlyTheFormulaColumnsForRoomPosts()
    {
        using var context = Probe();
        var roomId = Guid.NewGuid();

        var sql = context.Set<DbPost>()
            .Where(p => p.RoomId == roomId)
            .ProjectToPost()
            .ToQueryString();

        AssertNarrow(sql);
    }

    [Fact]
    public void FetchOnlyTheFormulaColumnsForTheGamesCatalog()
    {
        using var context = Probe();

        var sql = context.Set<DbGame>()
            .ProjectToGame()
            .ToQueryString();

        AssertNarrow(sql);
    }
}
