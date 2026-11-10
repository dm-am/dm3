using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The rule is a predicate beside the roles (IsBlacklisted) and the refusal the method next to it
/// (ThrowIfBlacklisted). It closes the discussion and the application of a game and the comments
/// of a blog, so a new closure is decided here.
/// </remarks>
public class BlacklistRuleShould
{
    // IsBlocked stays forbidden as a second way of asking; IsBlockedAsync is the personal list.
    private static readonly Regex Spelling = new(
        @"BlacklistedUsers\.Any\(|BlacklistedUserIds\.Contains\(|RefusalMessage\.BlacklistedFrom(?:Game|Blog)\b|\.IsBlocked\(",
        RegexOptions.Compiled);

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
}
