using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The blacklist of a game and the blacklist of a blog are each spelled in one
/// place, refuse in one sentence, and are asked only by the few actions they
/// close.
/// </summary>
/// <remarks>
/// One rule was spelled three ways and answered in three voices. The game asked
/// it as an inline Any over BlacklistedUsers, as a repository query and through a
/// subscription guard; the blog as a Contains over BlacklistedUserIds in four
/// services and a query in a fifth. Some of those places refused in the list's
/// words, some in the generic "not enough rights", and one of them refused
/// something the list was never meant to close: a subscription. Each copy was
/// right about its own action and nothing held them to each other.
///
/// Now the rule is a predicate beside the roles (GameRoleExtensions.IsBlacklisted,
/// BlogRoleExtensions.IsBlacklisted) and the refusal is the method next to it,
/// ThrowIfBlacklisted, which is the only thing that says "Вы в черном списке ...".
/// The check is textual for the reason the neighbouring ownership gates are: the
/// thing to keep out is a spelling that compiles anywhere.
///
/// The list then grew to close posts, chat rooms, turn marks, reviews, ratings,
/// likes and the return of a character, each of which another tool already
/// decides: the rooms and the roster, the moderators, the owner's own moves.
/// The owner cut it back on 05.10 to two closures for a game (the discussion and
/// the application) and one for a blog (comments). The third fact holds that
/// line: the predicate is asked by the places that close those and by nobody
/// else, so a new closure is a decision made here and not a line slipped into a
/// service.
/// </remarks>
public class BlacklistRuleShould
{
    /// <summary>
    /// The spellings of the rule and of its refusal.
    /// </summary>
    /// <remarks>
    /// A call of IsBlocked is the repository query the two lists used to carry
    /// beside the predicate. The query is gone, and its spelling stays here so
    /// that it does not come back as a second way of asking. The personal list's
    /// checker is IsBlockedAsync, which is a different list, asked in its own
    /// places, and the open parenthesis keeps it out.
    /// </remarks>
    private static readonly Regex Spelling = new(
        @"BlacklistedUsers\.Any\(|BlacklistedUserIds\.Contains\(|RefusalMessage\.BlacklistedFrom(?:Game|Blog)\b|\.IsBlocked\(",
        RegexOptions.Compiled);

    /// <summary>The two files the rule lives in.</summary>
    private static readonly string[] Owners = ["GameRoleExtensions.cs", "BlogRoleExtensions.cs"];

    [Fact]
    public void BeSpelledOnlyWhereTheRuleLives()
    {
        var carriers = RepositoryFiles.ProductionSources()
            .Where(path => !Owners.Contains(Path.GetFileName(path)))
            .Where(path => Spelling.IsMatch(SourceText.ReadCode(path)))
            .Select(path => Path.GetRelativePath(DM.Testing.RepositoryLayout.Root, path))
            .ToList();

        carriers.Should().BeEmpty(
            "a blacklist is asked through IsBlacklisted and refused through ThrowIfBlacklisted; a copy " +
            "of either is the one whose next revision answers the same person in other words");
    }

    [Fact]
    public void BeSpelledWhereItLives()
    {
        // The rule above passes vacuously if the owners are renamed away or stop
        // carrying the spelling; both of them must still hold it.
        foreach (var owner in Owners)
        {
            var path = RepositoryFiles.ProductionSources().Single(p => Path.GetFileName(p) == owner);
            Spelling.IsMatch(SourceText.ReadCode(path)).Should().BeTrue($"{owner} is where the rule lives");
        }
    }

    /// <summary>
    /// The places that ask the predicate, and why each may.
    /// </summary>
    private static readonly Dictionary<string, string> Askers = new()
    {
        ["GameRoleExtensions.cs"] = "the rule itself",
        ["BlogRoleExtensions.cs"] = "the rule itself",
        ["GameIntentionResolver.cs"] = "CreateComment and CreateCharacter, the two closures of a game",
        ["BlogIntentionResolver.cs"] = "CreateComment, the closure of a blog",
        ["GameCommentService.cs"] = "a comment in the game's discussion and the edit of one's own",
        ["BlogCommentService.cs"] = "a comment in the blog and the edit of one's own",
        ["PublicationCommentService.cs"] = "a comment under a publication and the edit of one's own",
        ["CharacterService.cs"] = "an application and the edit of one's own",
        ["GameBlacklistService.cs"] = "managing the list: already on it, not on it",
        ["BlogBlacklistService.cs"] = "managing the list: already on it, not on it",
        ["GameMapper.cs"] = "the viewer flag on the game's details",
        ["BlogMapper.cs"] = "the viewer flag on the blog",
    };

    /// <summary>A call of the predicate or of its refusal.</summary>
    private static readonly Regex Asking = new(@"\b(?:IsBlacklisted|ThrowIfBlacklisted)\(", RegexOptions.Compiled);

    [Fact]
    public void BeAskedOnlyByWhatItCloses()
    {
        var askers = RepositoryFiles.ProductionSources()
            .Where(path => Asking.IsMatch(SourceText.ReadCode(path)))
            .Select(Path.GetFileName)
            .ToList();

        askers.Except(Askers.Keys).Should().BeEmpty(
            "the game's list closes the discussion and the application, the blog's closes comments; " +
            "anything else is decided by the tool that owns it, and a new closure is a change to this list");
        Askers.Keys.Except(askers).Should().BeEmpty(
            "every place named here still asks the list, or the name is stale and lets the next file in");
    }

    /// <summary>
    /// Subscribing is refused by no target: the guards a module once put in front
    /// of it are gone, and none may come back under the old names.
    /// </summary>
    [Fact]
    public void LeaveTheRetiredGuardsUnasked()
    {
        var names = new[] { "ISubscriptionTargetGuard", "GameSubscriptionGuard", "BlogSubscriptionGuard" };

        var askers = new List<string>();
        foreach (var path in RepositoryFiles.ProductionSources())
        {
            var code = SourceText.ReadCode(path);
            askers.AddRange(names
                .Where(name => code.Contains(name, StringComparison.Ordinal))
                .Select(name => $"{Path.GetRelativePath(DM.Testing.RepositoryLayout.Root, path)}: {name}"));
        }

        askers.Should().BeEmpty(
            "subscribing is not refused by any target any more, so nothing may construct or ask a guard");
    }
}
