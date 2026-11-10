using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The list that decides what runs without asking stays narrow where it matters.
/// </summary>
/// <remarks>
/// A wildcard entry pre-approves every form of a verb, the ones the hook refuses included.
/// Read and build commands stay wide: they write nothing outside the checkout.
/// </remarks>
public class AgentPermissionsShould
{
    // A verb that either loses work, reaches the network, or runs whatever it is handed.
    private static readonly string[] Dangerous =
    [
        "git", "rm", "del", "curl", "wget", "ping", "kill", "pkill", "taskkill",
        "bash", "sh", "powershell", "powershell.exe", "pwsh", "cmd", "cmd.exe",
        "winget", "npm publish", "docker system prune",
    ];


    private static IReadOnlyList<string> Entries(string settingsFile, string section)
    {
        var path = Path.Combine(RepositoryLayout.Root, ".claude", settingsFile);
        if (!File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("permissions", out var permissions) ||
            !permissions.TryGetProperty(section, out var entries))
        {
            return [];
        }

        return entries.EnumerateArray().Select(entry => entry.GetString() ?? string.Empty).ToList();
    }

    // Matches "Bash(verb:*)" and "Bash(verb *)": the two spellings of "anything this verb can be
    // handed".
    private static string? BareWildcardVerb(string entry)
    {
        var match = Regex.Match(entry, @"^Bash\(([^:()\s]+(?:\.exe)?)\s*(?::\*|\*)\)$");
        return match.Success ? match.Groups[1].Value : null;
    }

    [Fact]
    public void PreApproveNoDangerousVerbWithAWildcard()
    {
        var offenders = new List<string>();

        foreach (var file in new[] { "settings.json", "settings.local.json" })
        {
            offenders.AddRange(Entries(file, "allow")
                .Where(entry =>
                {
                    var verb = BareWildcardVerb(entry);
                    return verb != null && Dangerous.Contains(verb, StringComparer.OrdinalIgnoreCase);
                })
                .Select(entry => $"{file}: {entry}"));
        }

        offenders.Should().BeEmpty(
            "a wildcard on one of these verbs approves every form of it in advance, " +
            "including the forms the hook exists to refuse; the narrow spelling " +
            "(\"Bash(git log:*)\", \"Bash(curl http://localhost:*)\") approves the use " +
            "that is actually needed");
    }

    private static string HookPath =>
        Path.Combine(RepositoryLayout.Root, ".claude", "hooks", "block-dangerous-git.js");

    private sealed record HookRule(string Source, Regex Pattern)
    {
        // Whether a flag further along the command line calls the refusal off, the way --staged
        // does for restore.
        public bool CalledOffByALaterFlag => Source.Contains("(?!", StringComparison.Ordinal);

        public bool Refuses(string command) => Pattern.IsMatch(command);
    }

    // The case flag is read separately: one rule refuses "-D" and lets "-d" through.
    private static IReadOnlyList<HookRule> HookRules()
    {
        var array = Regex.Match(
            File.ReadAllText(HookPath), @"const RULES = \[(.*?)\r?\n\];", RegexOptions.Singleline);

        array.Success.Should().BeTrue(
            "the refusals are read out of the hook, so they stay a literal RULES array in it");

        return Regex.Matches(array.Groups[1].Value, @"pattern:\s*/((?:[^/\\\r\n]|\\.)+)/([a-z]*)")
            .Select(rule => new HookRule(
                rule.Groups[1].Value,
                new Regex(
                    rule.Groups[1].Value,
                    rule.Groups[2].Value.Contains('i') ? RegexOptions.IgnoreCase : RegexOptions.None)))
            .ToList();
    }

    // What a command line spells itself with, as opposed to regex syntax.
    private static bool IsPlain(char symbol) =>
        char.IsLetterOrDigit(symbol) || symbol is ' ' or '-' or '_' or '=' or ':' or '/';

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string[] Words(string command) =>
        command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static bool IsQuantifier(char symbol) => symbol is '*' or '+' or '?' or '{';

    // "\s+" reads as one space, "\b" as nothing.
    private static string LeadingLiteral(string pattern, int start, out int stopped)
    {
        var text = new StringBuilder();
        var index = start;

        while (index < pattern.Length)
        {
            var symbol = pattern[index];

            if (symbol == '\\' && index + 1 < pattern.Length)
            {
                var escaped = pattern[index + 1];

                if (escaped == 'b')
                {
                    index += 2;
                    continue;
                }

                if (escaped == 's')
                {
                    index += 2;
                    text.Append(' ');
                    if (index < pattern.Length && pattern[index] is '+' or '*')
                    {
                        index++;
                    }

                    continue;
                }

                if (char.IsLetterOrDigit(escaped))
                {
                    break;
                }

                text.Append(escaped);
                index += 2;
                continue;
            }

            if (!IsPlain(symbol) ||
                (index + 1 < pattern.Length && IsQuantifier(pattern[index + 1])))
            {
                break;
            }

            text.Append(symbol);
            index++;
        }

        stopped = index;
        return Collapse(text.ToString());
    }

    // The one text a fragment matches, or null when it matches more than one.
    private static string? LiteralOf(string fragment)
    {
        var literal = LeadingLiteral(fragment, 0, out var stopped);
        return stopped == fragment.Length ? literal : null;
    }

    private static int GroupEnd(string pattern, int start)
    {
        var depth = 0;
        var inClass = false;

        for (var index = start; index < pattern.Length; index++)
        {
            var symbol = pattern[index];

            if (symbol == '\\')
            {
                index++;
            }
            else if (inClass)
            {
                inClass = symbol != ']';
            }
            else if (symbol == '[')
            {
                inClass = true;
            }
            else if (symbol == '(')
            {
                depth++;
            }
            else if (symbol == ')' && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    // Stops at the first construct that is not literal: a permission entry matches a prefix of the
    // command line only.
    private static IReadOnlyList<string> CommandNames(string pattern)
    {
        if (!pattern.StartsWith('^'))
        {
            return [];
        }

        var head = LeadingLiteral(pattern, 1, out var stopped);
        var names = new List<string> { head };
        var end = stopped < pattern.Length && pattern[stopped] == '('
            ? GroupEnd(pattern, stopped)
            : -1;

        if (end > 0 && (end + 1 == pattern.Length || !IsQuantifier(pattern[end + 1])))
        {
            var body = pattern[(stopped + 1)..end];
            body = body.StartsWith("?:", StringComparison.Ordinal) ? body[2..] : body;

            // "(?" other than "(?:" is a lookaround: a condition on the rest of the line, not the next word.
            List<string?> branches = body.StartsWith('?')
                ? []
                : body.Split('|').Select(LiteralOf).ToList();

            if (branches.Count > 0 && branches.All(branch => !string.IsNullOrEmpty(branch)))
            {
                names = branches.Select(branch => Collapse($"{head} {branch}")).ToList();
            }
        }

        return names.Where(name => name.Length > 0).Distinct(StringComparer.Ordinal).ToList();
    }

    // The command a deny entry refuses, out of "Bash(git checkout:*)".
    private static string? DeniedCommand(string entry)
    {
        var match = Regex.Match(entry, @"^Bash\((.+?)\s*(?::\*|\*)?\)$");
        return match.Success ? Collapse(match.Groups[1].Value) : null;
    }

    // Word for word, because "-d" is not "-D" and the hook allows one of them.
    private static bool Opens(string command, string name)
    {
        var words = Words(command);
        var opening = Words(name);

        return words.Length >= opening.Length &&
               words.Take(opening.Length).SequenceEqual(opening, StringComparer.Ordinal);
    }

    // An entry passes when it spells a command the rule refuses or is the bare subcommand; one for
    // a form the rule lets through stands for no refusal.
    private static bool Names(HookRule rule, string name, string denied) =>
        string.Equals(denied, name, StringComparison.Ordinal) ||
        (rule.Refuses(denied) && Opens(denied, name));

    private static IReadOnlyList<string> DeniedCommands() =>
        Entries("settings.json", "deny")
            .Select(DeniedCommand)
            .Where(command => !string.IsNullOrEmpty(command))
            .Select(command => command!)
            .ToList();

    /// <summary>
    /// The refusals are written down rather than left implicit in the absence of an
    /// approval, because the next reader of this file learns the rule from what it
    /// says, not from what it omits.
    /// </summary>
    /// <remarks>
    /// The names are read out of the hook, the one place a refusal is declared, and only the
    /// tracked settings file counts. A rule a later flag calls off is left to the hook alone.
    /// </remarks>
    [Fact]
    public void NameTheRefusalsTheHookEnforces()
    {
        var denied = DeniedCommands();

        var unnamed = HookRules()
            .Where(rule => !rule.CalledOffByALaterFlag)
            .SelectMany(rule => CommandNames(rule.Source).Select(name => (Rule: rule, Name: name)))
            .Where(refusal => !denied.Any(entry => Names(refusal.Rule, refusal.Name, entry)))
            .Select(refusal => $"{refusal.Name} (rule /{refusal.Rule.Source}/)")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(refusal => refusal, StringComparer.Ordinal)
            .ToList();

        unnamed.Should().BeEmpty(
            "the hook decides what is forbidden, and a command it refuses that deny does " +
            "not name reads as merely unapproved to every reader that never runs the hook. " +
            "Listed is each such command with the rule that refuses it: add a deny entry " +
            "spelling a form that rule matches, which forbids nothing the hook allows " +
            "(\"Bash(git branch -D:*)\", \"Bash(git push --force:*)\"), or the bare name, " +
            "which forbids the whole subcommand");
    }

    /// <summary>
    /// A refusal that lives in the settings alone is enforced by nothing.
    /// </summary>
    /// <remarks>Only git is compared: other refusals belong to other hooks.</remarks>
    [Fact]
    public void HoldNoGitRefusalTheHookDoesNot()
    {
        var rules = HookRules();

        var unenforced = DeniedCommands()
            .Where(denied => Words(denied).FirstOrDefault() == "git")
            .Where(denied => !rules.Any(rule =>
                CommandNames(rule.Source).Any(name => Names(rule, name, denied))))
            .OrderBy(denied => denied, StringComparer.Ordinal)
            .ToList();

        unenforced.Should().BeEmpty(
            "the hook is the one place a git refusal is declared, and these are denied " +
            "here without a rule that refuses them: an agent reading the settings believes " +
            "they are blocked, and nothing blocks them");
    }

    /// <summary>
    /// A rule this cannot read is a rule the settings is never compared against.
    /// </summary>
    /// <remarks>A read of the hook that finds nothing would look like full coverage above.</remarks>
    [Fact]
    public void ReadEveryRuleTheHookHolds()
    {
        var rules = HookRules();

        rules.Should().NotBeEmpty(
            "the hook is where the refusals live, and a parse that finds none of them " +
            "agrees with any settings file there is");

        rules.Where(rule => CommandNames(rule.Source).Count == 0).Select(rule => rule.Source)
            .Should().BeEmpty(
                "the settings can only be checked against a rule whose command is readable: " +
                "anchor the pattern at ^ and keep the words before its first flag literal, or " +
                "the rule is left to the hook alone");
    }

    // The forms the hook lets through on purpose, spelled the way its own test spells them, plus
    // the bare verb a blanket entry would name.
    private static readonly string[] Allowed =
    [
        "git", "git status", "git stash", "git restore --staged src/file.cs",
        "git reset --soft origin/dev", "git clean -n", "git push origin dev",
        "git branch -d merged",
    ];

    /// <summary>
    /// A comparison that cannot reject accepts anything, including the entry that
    /// forbids the way out.
    /// </summary>
    /// <remarks>"Bash(git restore --staged:*)" and "Bash(git:*)" must not stand for a refusal.</remarks>
    [Fact]
    public void AcceptNoEntryThatNamesACommandTheHookAllows()
    {
        var rules = HookRules();
        rules.Should().NotBeEmpty("the rule below is written in terms of those rules");

        var accepted = rules
            .SelectMany(rule => CommandNames(rule.Source).Select(name => (Rule: rule, Name: name)))
            .SelectMany(refusal => Allowed
                .Where(command => Names(refusal.Rule, refusal.Name, command))
                .Select(command => $"Bash({command}:*) for {refusal.Name}"))
            .OrderBy(entry => entry, StringComparer.Ordinal)
            .ToList();

        accepted.Should().BeEmpty(
            "these commands pass the hook by its own test, so a deny entry spelling one " +
            "of them refuses nothing the hook refuses and must not count as naming a " +
            "refusal");
    }
}
