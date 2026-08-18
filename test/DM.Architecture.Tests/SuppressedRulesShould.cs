using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Every check switched off in product source is listed here, with the reason it
/// is off.
/// </summary>
/// <remarks>
/// A rule can be dealt with two ways: fix what it points at, or write the line
/// that tells it to look away. The second is sometimes right, and it is always
/// silent - the build goes green and nothing records that a check stopped
/// applying or why. Over a year that is how a suite of checks becomes decoration:
/// still there, switched off everywhere it was inconvenient.
///
/// This test is the register. It is deliberately not a document: a list of what
/// is currently switched off is a snapshot of state, and a snapshot in prose
/// drifts from the tree on the next edit and then misinforms. A test cannot
/// drift - it fails.
///
/// Two scopes are left out on purpose:
///
/// Generated migrations. EF writes those files and puts its own pragmas in them;
/// the repository does not author that code and regenerating it would restore
/// them.
///
/// The test tree. Its only occurrence is a string constant in SchemaSources that
/// names the pragma in order to find where the generated model starts - not a
/// suppression at all. A scanner that counted string literals would be reporting
/// on the very code that reads them, and tests are not shipped besides.
/// </remarks>
public class SuppressedRulesShould
{
    /// <summary>
    /// One switched-off check: where it is, which rule, and why it stays off.
    /// </summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Rule">The rule that is switched off.</param>
    /// <param name="Why">Why fixing the code is not the answer here.</param>
    private sealed record Suppression(string File, string Rule, string Why);

    private static readonly Suppression[] Allowed =
    [
        new("src/DM.Web.Client/src/entities/game/ui/CharacterCard.vue",
            "vuejs-accessibility/click-events-have-key-events",
            "The rule wants a key handler beside the click, and reads one element at a " +
            "time. The click is on the header; the keyboard path is the chevron button " +
            "inside it, which carries aria-expanded and is reachable by tab. The path " +
            "exists - the rule cannot see it from where it is looking."),

        new("src/DM.Web.Client/src/entities/user/ui/UsernameInput.vue",
            "no-control-regex",
            "The rule forbids control characters in a pattern; this pattern exists to " +
            "reject them. They are forbidden in usernames, and matching them is the " +
            "whole point - see docs/conventions/USERNAME_POLICY.md."),

        new("src/DM.Web.Client/e2e/fixtures/auth.ts",
            "no-empty-pattern",
            "Playwright reads a fixture's dependencies from its destructuring pattern, so " +
            "one that depends on nothing still has to declare the empty pattern the rule " +
            "objects to. Removing it changes the signature the framework matches on."),
    ];

    private static readonly string[] Markers =
    [
        "eslint-disable",
        "#pragma warning disable",
    ];

    /// <summary>
    /// Nothing is switched off that is not listed above, and nothing is listed that
    /// is no longer switched off.
    /// </summary>
    /// <remarks>
    /// Checked from both ends so the list cannot rot into a licence. An entry for a
    /// suppression that has since been removed is a reason nobody has to give any
    /// more, and leaving it invites the next one to be added under it.
    /// </remarks>
    [Fact]
    public void CarryAReasonForEveryCheckThatIsSwitchedOff()
    {
        var found = Scan().ToArray();

        var listed = Allowed.Select(entry => entry.File).ToHashSet(StringComparer.Ordinal);
        var undeclared = found.Where(file => !listed.Contains(file)).Distinct().ToArray();
        var stale = listed.Where(file => !found.Contains(file)).ToArray();

        undeclared.Should().BeEmpty(
            "a check switched off without a written reason is a check nobody decided to " +
            "switch off: add it to Allowed with the reason, or fix what it points at");
        stale.Should().BeEmpty(
            "the suppression is gone, so the reason for it is spent: drop the entry " +
            "rather than leave it standing for the next one to be added under");
    }

    /// <summary>
    /// A reason has to say something. An entry with a placeholder for prose is the
    /// same silence this test exists to end, spelled differently.
    /// </summary>
    [Fact]
    public void SpellOutEachReasonRatherThanNameTheRuleAgain()
    {
        foreach (var entry in Allowed)
        {
            entry.Why.Should().NotBeNullOrWhiteSpace();
            entry.Why.Length.Should().BeGreaterThan(
                entry.Rule.Length,
                "the reason has to explain the exception, and a line no longer than the " +
                "rule's own name explains nothing");
        }
    }

    private static IEnumerable<string> Scan()
    {
        var root = DM.Testing.RepositoryLayout.RootDirectory;
        var source = new DirectoryInfo(Path.Combine(root.FullName, "src"));

        foreach (var file in source.EnumerateFiles("*.*", SearchOption.AllDirectories))
        {
            if (!IsProductSource(file)) continue;

            var text = File.ReadAllText(file.FullName);
            if (!Markers.Any(marker => text.Contains(marker, StringComparison.Ordinal))) continue;

            yield return Path.GetRelativePath(root.FullName, file.FullName).Replace('\\', '/');
        }
    }

    private static bool IsProductSource(FileInfo file)
    {
        var extension = file.Extension.ToLowerInvariant();
        if (extension is not (".cs" or ".ts" or ".vue")) return false;

        var path = file.FullName.Replace('\\', '/');
        // Build output and dependencies are not ours; migrations are EF's own.
        return !path.Contains("/node_modules/", StringComparison.Ordinal)
            && !path.Contains("/bin/", StringComparison.Ordinal)
            && !path.Contains("/obj/", StringComparison.Ordinal)
            && !path.Contains("/dist/", StringComparison.Ordinal)
            && !path.Contains("/Migrations/", StringComparison.Ordinal);
    }
}
