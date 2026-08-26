using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The list that decides what runs without asking stays narrow where it matters.
/// </summary>
/// <remarks>
/// The hook in .claude/hooks refuses the commands that lose uncommitted work, and
/// it is the one place they are declared: it reads the whole command line, which
/// is what tells "git restore --staged" apart from "git restore .". A wildcard
/// entry here is the other side of the same door: it pre-approves every form of a
/// verb, including the ones nobody thought about when the entry was written, and
/// the whole list had one for git, one for rm and one for the shell interpreter at
/// the same time.
///
/// Read and build commands stay wide on purpose. They do not write outside the
/// checkout and do not reach the network, so approving them one by one buys
/// nothing but interruptions.
/// </remarks>
public class AgentPermissionsShould
{
    /// <summary>
    /// A verb that either loses work, reaches the network, or runs whatever it is
    /// handed. None of them may appear with a bare wildcard.
    /// </summary>
    private static readonly string[] Dangerous =
    [
        "git", "rm", "del", "curl", "wget", "ping", "kill", "pkill", "taskkill",
        "bash", "sh", "powershell", "powershell.exe", "pwsh", "cmd", "cmd.exe",
        "winget", "npm publish", "docker system prune",
    ];

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static IReadOnlyList<string> Entries(string settingsFile, string section)
    {
        var path = Path.Combine(RepositoryRoot, ".claude", settingsFile);
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

    /// <summary>
    /// Matches "Bash(verb:*)" and "Bash(verb *)": the two spellings of "anything
    /// this verb can be handed".
    /// </summary>
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

    /// <summary>The hook that refuses the commands which lose uncommitted work.</summary>
    private static string HookPath =>
        Path.Combine(RepositoryRoot, ".claude", "hooks", "block-dangerous-git.js");

    /// <summary>One rule of the hook: a refusal, and the pattern that spells it.</summary>
    private sealed record HookRule(string Source, Regex Pattern)
    {
        /// <summary>
        /// Whether a flag further along the command line calls the refusal off, the
        /// way --staged does for restore.
        /// </summary>
        public bool CalledOffByALaterFlag => Source.Contains("(?!", StringComparison.Ordinal);

        /// <summary>Whether the hook refuses this command line by this rule.</summary>
        public bool Refuses(string command) => Pattern.IsMatch(command);
    }

    /// <summary>
    /// Every rule the hook holds, in file order.
    /// </summary>
    /// <remarks>
    /// The patterns are ordinary regex literals, so each one carries over as
    /// written and only the case-insensitivity flag has to be read off separately.
    /// It is not decoration: the one rule without it refuses "-D" and lets "-d"
    /// through, and a comparison that lost the flag would call the safe form
    /// forbidden.
    /// </remarks>
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

    /// <summary>What a command line spells itself with, as opposed to regex syntax.</summary>
    private static bool IsPlain(char symbol) =>
        char.IsLetterOrDigit(symbol) || symbol is ' ' or '-' or '_' or '=' or ':' or '/';

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ").Trim();

    private static string[] Words(string command) =>
        command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>A symbol that makes what stands before it optional or repeated.</summary>
    private static bool IsQuantifier(char symbol) => symbol is '*' or '+' or '?' or '{';

    /// <summary>
    /// The literal text a pattern carries from <paramref name="start"/> on, up to
    /// the first construct that matches more than one text. That position comes
    /// back in <paramref name="stopped"/>.
    /// </summary>
    /// <remarks>
    /// "\s+" is a single space: it is how the hook spells the gap between the words
    /// of a command. "\b" is nothing at all: it separates words without being one.
    /// </remarks>
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

    /// <summary>The one text a fragment matches, or null when it matches more than one.</summary>
    private static string? LiteralOf(string fragment)
    {
        var literal = LeadingLiteral(fragment, 0, out var stopped);
        return stopped == fragment.Length ? literal : null;
    }

    /// <summary>Index of the ")" that closes the group opening at <paramref name="start"/>.</summary>
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

    /// <summary>
    /// The commands a rule refuses, spelled the way a command line spells them: the
    /// literal text the pattern opens with, one name per branch when that text runs
    /// into an alternation of literals.
    /// </summary>
    /// <remarks>
    /// Everything from the first construct that is not literal text on is left out,
    /// because that is where the pattern stops describing the head of a command
    /// line and starts describing what may stand anywhere in the rest of it: "git
    /// clean", not the forcing flag the rule actually keys on. A permission entry
    /// cannot say the second thing at all, because it matches a prefix of the
    /// command line and nothing else.
    /// </remarks>
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

            // A group opening with "?" that is not "?:" is a lookaround: it matches
            // no text of its own, and what follows it is a condition on the rest of
            // the line rather than the next word of the command.
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

    /// <summary>The command a deny entry refuses, out of "Bash(git checkout:*)".</summary>
    private static string? DeniedCommand(string entry)
    {
        var match = Regex.Match(entry, @"^Bash\((.+?)\s*(?::\*|\*)?\)$");
        return match.Success ? Collapse(match.Groups[1].Value) : null;
    }

    /// <summary>Word for word, because "-d" is not "-D" and the hook allows one of them.</summary>
    private static bool Opens(string command, string name)
    {
        var words = Words(command);
        var opening = Words(name);

        return words.Length >= opening.Length &&
               words.Take(opening.Length).SequenceEqual(opening, StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether a deny entry is the settings side of one of a rule's refusals.
    /// </summary>
    /// <remarks>
    /// Two shapes pass. The entry may spell a command the rule itself refuses,
    /// which is how a rule keyed on a flag gets named without the entry reaching
    /// past it: everything such an entry denies, the hook refuses too. Or it may be
    /// the bare name, which denies the whole subcommand, more than the rule refuses
    /// and never less, the way "git clean" is denied here today.
    ///
    /// What does not pass is an entry for a form the rule lets through.
    /// "Bash(git restore --staged:*)" stands for no refusal, because the rule does
    /// not match it and it is not the name, and a blanket "Bash(git:*)" stands for
    /// none either. Without that the check would accept an entry forbidding exactly
    /// what the hook prints as the way out, and report the refusal as written down.
    /// </remarks>
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
    /// Two lists of forbidden commands drift the moment one of them grows, and this
    /// pair did: the hook refuses restore, force push, branch deletion and garbage
    /// collection on top of the four commands deny names, and the gate that was
    /// meant to compare them held the same four names of its own, so it agreed with
    /// the settings whatever the hook said.
    ///
    /// The names are therefore not written here any more. They are read out of the
    /// hook, which makes it the one place a refusal is declared: a rule added there
    /// turns this red until deny names it, and the failure spells out which command
    /// and which rule.
    ///
    /// A rule whose refusal a later flag calls off is left to the hook alone, and
    /// that is a limit of the settings rather than a hole here. A deny entry matches
    /// the head of a command line, and the flag that clears the refusal stands after
    /// that head. So an entry for such a rule either stops before the flag and
    /// forbids the form the hook allows, which for restore is the "--staged" the
    /// hook itself prints as the way to unstage, or it names the flag and forbids
    /// nothing the rule refuses.
    ///
    /// Only the tracked settings file counts. settings.local.json is gitignored, so
    /// a refusal living there alone holds on one machine and on no other.
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
    /// <remarks>
    /// This is the same rule read from the other end. The settings are a list a
    /// reader consults; the hook is what actually runs and stops the command, so a
    /// git entry here that answers to no rule promises a refusal nobody makes. When
    /// this goes red the entry is not the thing to delete: the rule it needs is the
    /// thing to write.
    ///
    /// Only git is compared. Other refusals in this file belong to other hooks,
    /// which hold their own rules in their own files: the migration guard is one.
    /// </remarks>
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
    /// <remarks>
    /// The comparison is a read of another language's source, and it fails the two
    /// ways such a read fails: the array moves and nothing is found at all, or one
    /// pattern stops opening with the text of a command and that rule leaves the
    /// comparison alone. In the tests above both look like a settings file that
    /// covers everything, so they are stated here instead.
    /// </remarks>
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

    /// <summary>
    /// The forms the hook lets through on purpose, spelled the way its own test
    /// spells them, plus the bare verb a blanket entry would name.
    /// </summary>
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
    /// <remarks>
    /// The check above reads deny against the hook, and the cheap way to satisfy it
    /// is an entry that names a command near the forbidden one instead of the
    /// forbidden one: "Bash(git restore --staged:*)" against the restore rule,
    /// "Bash(git:*)" against all of them. Both would report the refusal as written
    /// down while forbidding something else, and one of them forbids what the hook
    /// prints as the way out. None of these may stand for a refusal.
    /// </remarks>
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
