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
/// A command a document gives to run exists: its program is one a reader of this
/// repository has, its script is in the tree, and its subcommand is one the script
/// takes.
/// </summary>
/// <remarks>
/// A block of shell in a guide is pasted, not read, and a command that no longer
/// exists fails in the reader's terminal rather than in review. Whether a command
/// does what the prose around it says cannot be checked; whether there is anything
/// to run can.
///
/// What is read: the blocks of code a document marks as shell (bash, sh, shell,
/// console, powershell, ps1, pwsh), line by line, a line continued with a
/// backslash or a backtick read once. A line is cut at an unquoted "#" and into
/// commands at unquoted "&amp;&amp;", "||", "|" and ";". Leading assignments of the
/// environment and a leading sudo are stepped over, and "cd" is followed, so a
/// script is looked for where the block stands: a path through a folder named dm3
/// is the checkout (the server guides clone it to /opt/dm3), any other absolute
/// path is outside the tree and its scripts are not looked for.
///
/// What is checked: the program is on <see cref="KnownPrograms"/> or is a path; a
/// script, whether run by its path or handed to bash, sh, powershell -File, node or
/// "." is a file of the tree; scripts/dm.ps1 and scripts/dm.sh are given a command
/// their ValidateSet or their case list takes; "cd" enters a folder of the tree;
/// "npm run" names a script of the package.json in the folder the block stands
/// in. A placeholder in angle brackets is not a path and is not looked for.
///
/// Recorded per document at its count (see <see cref="HandwrittenDocuments"/> for
/// the contract of the register).
/// </remarks>
public class DocumentedCommandsShould
{
    private static readonly string[] ShellLanguages = ["bash", "sh", "shell", "console", "powershell", "ps1", "pwsh"];

    /// <summary>
    /// The programs a reader of this repository is expected to have: the toolchain
    /// the guides set up, the shells, the builtins that change where a block
    /// stands, and the four the server guides use on a Linux host (cp, systemctl,
    /// certbot, jq). Anything else is a program a guide asks for without saying so.
    /// </summary>
    private static readonly string[] KnownPrograms =
    [
        "git", "docker", "dotnet", "npm", "npx", "node", "bash", "sh", "powershell", "pwsh", "curl",
        "cd", "export", "source", ".", "cp", "systemctl", "certbot", "jq",
    ];

    /// <summary>The management scripts whose first argument is a command, and where they list them.</summary>
    private static readonly Dictionary<string, Func<string, IEnumerable<string>>> Dispatchers = new(StringComparer.Ordinal)
    {
        ["scripts/dm.ps1"] = text => Regex.Match(text, @"ValidateSet\(([^)]*)\)").Groups[1].Value
            .Split(',').Select(item => item.Trim().Trim('\'', '"')),
        ["scripts/dm.sh"] = text => Regex.Matches(
                Regex.Match(text, @"case ""\$\{1[^\n]*\n(?<body>.*?)\nesac", RegexOptions.Singleline).Groups["body"].Value,
                @"^\s*(?<labels>[\w|*-]+)\)", RegexOptions.Multiline)
            .SelectMany(match => match.Groups["labels"].Value.Split('|')),
    };

    private static readonly Regex Assignment = new(@"^[A-Za-z_]\w*=", RegexOptions.Compiled);

    /// <summary>A document holding commands that do not exist, at their count.</summary>
    /// <param name="File">Path from the repository root, forward slashes.</param>
    /// <param name="Count">How many such commands the document holds.</param>
    private sealed record Recorded(string File, int Count);

    private static readonly Recorded[] Register =
    [
        new(".claude/skills/html-doc/SKILL.md", 4),
        new("docs/guides/LOCAL_SETUP.md", 1),
    ];

    private static readonly Lazy<HashSet<string>> TreeFiles = new(() => RepositoryFiles.CommittableFiles()
        .Select(HandwrittenDocuments.Relative)
        .ToHashSet(StringComparer.Ordinal));

    private static readonly Lazy<HashSet<string>> TreeFolders = new(() => TreeFiles.Value
        .SelectMany(file => file.Split('/').SkipLast(1).Select((_, i) => string.Join('/', file.Split('/').Take(i + 1))))
        .ToHashSet(StringComparer.Ordinal));

    [Fact]
    public void HoldEveryCommandThatDoesNotExistAtItsRecordedCount()
    {
        var found = new List<string>();
        var measured = new Dictionary<string, int>(StringComparer.Ordinal);
        var read = 0;
        foreach (var document in HandwrittenDocuments.All())
        {
            var problems = Blocks(document.Lines).SelectMany(block => { read += block.Count; return Problems(block); }).ToList();
            if (problems.Count > 0)
            {
                measured[document.Path] = problems.Count;
                found.AddRange(problems.Select(problem => $"  {document.Path}:{problem}"));
            }
        }

        read.Should().BeGreaterThan(50, "the guides hold some hundred lines of shell, and a walk that finds none checks nothing");

        var disagreements = HandwrittenDocuments.Disagreements(
            Register.ToDictionary(entry => entry.File, entry => entry.Count, StringComparer.Ordinal),
            measured,
            (file, count) => $"new(\"{file}\", {count}),");

        string.Join(Environment.NewLine, disagreements.Concat(disagreements.Count > 0 ? found : [])).Should().BeEmpty(
            "a command a guide gives is pasted into a terminal: point it at a script and a subcommand the tree " +
            "has, and lower the record the moment a document loses one");
    }

    /// <summary>What a command needs to exist.</summary>
    [Theory]
    [InlineData("powershell -File .\\scripts\\dm.ps1 start", true)]
    [InlineData("powershell -File .\\scripts\\dm.ps1 launch", false)]
    [InlineData("./scripts/dm.sh seed", true)]
    [InlineData("./scripts/dm.sh launch", false)]
    [InlineData("bash scripts/no-such-script.sh", false)]
    [InlineData("cd docker\nbash scripts/init-env.sh local", true)]
    [InlineData("cd /opt/dm3/docker && . scripts/_env.sh", true)]
    [InlineData("cd src/DM.Web.Client && npm install && npm run build", true)]
    [InlineData("cd src/DM.Web.Client\nnpm run no-such-script", false)]
    [InlineData("cd docker\ncd src/DM.Web.Client && npm run dev", false)]
    [InlineData("DM_REF=<tag>\ncurl -sSL https://example.org/x.sh | DM_REF=$DM_REF bash", true)]
    [InlineData("docker run --rm image \\\n  python3 -c 'a && b'", true)]
    [InlineData("python3 tool.py   # a program the guides never set up", false)]
    [InlineData("sudo certbot renew --dry-run", true)]
    public void TellACommandThatExistsFromOneThatDoesNot(string block, bool exists) =>
        Problems(HandwrittenDocuments.Lines(block).ToList()).Should().HaveCount(exists ? 0 : 1);

    /// <summary>The blocks of shell of a document, each as its lines.</summary>
    private static IEnumerable<List<HandwrittenDocuments.DocumentLine>> Blocks(
        IEnumerable<HandwrittenDocuments.DocumentLine> lines)
    {
        List<HandwrittenDocuments.DocumentLine>? block = null;
        var previous = -1;
        foreach (var line in lines)
        {
            var shell = line.Language is { } language && ShellLanguages.Contains(language, StringComparer.OrdinalIgnoreCase);
            if (block is not null && (!shell || line.Number != previous + 1))
            {
                yield return block;
                block = null;
            }

            if (shell)
            {
                (block ??= []).Add(line);
                previous = line.Number;
            }
        }

        if (block is not null)
        {
            yield return block;
        }
    }

    /// <summary>One "line: reason" per command of the block that does not exist.</summary>
    private static List<string> Problems(IReadOnlyList<HandwrittenDocuments.DocumentLine> block)
    {
        var problems = new List<string>();
        string? folder = string.Empty;
        var continued = false;
        foreach (var line in block)
        {
            var text = WithoutComment(line.Text).Trim();
            var skip = continued;
            continued = text.EndsWith('\\') || text.EndsWith('`');
            if (skip || text.Length == 0)
            {
                continue;
            }

            foreach (var command in Split(text, ["&&", "||", "|", ";"]))
            {
                var words = Split(command, [" ", "\t"])
                    .SkipWhile(word => Assignment.IsMatch(word) || word is "(" or "sudo")
                    .Select(word => word.Trim('"', '\''))
                    .Where(word => word is not ("(" or ")"))
                    .ToList();
                if (words.Count == 0)
                {
                    continue;
                }

                if (Check(words, ref folder) is { } reason)
                {
                    problems.Add($"{line.Number}: {reason}");
                }
            }
        }

        return problems;
    }

    /// <summary>Why the command does not exist, or null when it does; follows "cd".</summary>
    private static string? Check(List<string> words, ref string? folder)
    {
        var program = words[0];
        if (program == "cd")
        {
            folder = Enter(folder, words.ElementAtOrDefault(1) ?? string.Empty);
            if (folder is { Length: > 0 } && !TreeFolders.Value.Contains(folder))
            {
                folder = null;
                return $"cd {words[1]}: no such folder in the tree from where the block stands";
            }

            return null;
        }

        var pathLike = program.Contains('/') || program.Contains('\\');
        if (!pathLike && !KnownPrograms.Contains(program, StringComparer.Ordinal))
        {
            return $"\"{program}\" is not a program a reader of this repository is expected to have";
        }

        var scriptAt = pathLike ? 0 : ScriptArgument(program, words);
        if (scriptAt > 0 || pathLike)
        {
            return CheckScript(words[scriptAt], words.ElementAtOrDefault(scriptAt + 1), folder);
        }

        if (program == "npm" && words.ElementAtOrDefault(1) == "run" && words.Count > 2 && folder is not null)
        {
            var manifest = Path.Combine(folder, "package.json").Replace('\\', '/').TrimStart('/');
            return NpmScripts(manifest).Contains(words[2])
                ? null
                : $"npm run {words[2]}: no such script in {manifest}";
        }

        return null;
    }

    /// <summary>Where the program takes the script it runs, or 0 when it runs none.</summary>
    private static int ScriptArgument(string program, List<string> words)
    {
        var at = program switch
        {
            "bash" or "sh" or "." or "source" => words.FindIndex(1, word => !word.StartsWith('-')),
            "powershell" or "pwsh" => words.FindIndex(word => word.Equals("-File", StringComparison.OrdinalIgnoreCase)) + 1,
            "node" => words.FindIndex(1, word => Regex.IsMatch(word, @"\.(m|c)?js$")),
            _ => -1,
        };

        var takesCode = program is "bash" or "sh" && words.Contains("-c");
        return at <= 0 || at >= words.Count || takesCode ? 0 : at;
    }

    private static string? CheckScript(string script, string? argument, string? folder)
    {
        if (script.Contains('<') || folder is null)
        {
            return null;
        }

        var path = Enter(folder, script);
        if (path is null)
        {
            return null;
        }

        if (!TreeFiles.Value.Contains(path))
        {
            return $"{script}: no such file in the tree";
        }

        return Dispatchers.TryGetValue(path, out var commands) && argument is not null &&
               !commands(File.ReadAllText(Path.Combine(DM.Testing.RepositoryLayout.Root, path))).Contains(argument)
            ? $"{script} {argument}: the script takes no such command"
            : null;
    }

    /// <summary>
    /// The folder of the tree a path leads to from a folder, or null outside the
    /// tree: through a folder named dm3 it is the checkout.
    /// </summary>
    private static string? Enter(string? folder, string path)
    {
        var segments = path.Replace('\\', '/').Split('/').ToList();
        var checkout = segments.LastIndexOf("dm3");
        if (checkout >= 0)
        {
            segments = segments.Skip(checkout + 1).ToList();
            folder = string.Empty;
        }
        else if (path.StartsWith('/') || path.StartsWith('~') || folder is null)
        {
            return null;
        }

        var result = folder.Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        foreach (var segment in segments.Where(segment => segment is not ("" or ".")))
        {
            if (segment == "..")
            {
                if (result.Count == 0)
                {
                    return null;
                }

                result.RemoveAt(result.Count - 1);
            }
            else
            {
                result.Add(segment);
            }
        }

        return string.Join('/', result);
    }

    private static IReadOnlySet<string> NpmScripts(string manifest)
    {
        var full = Path.Combine(DM.Testing.RepositoryLayout.Root, manifest);
        if (!File.Exists(full))
        {
            return new HashSet<string>();
        }

        using var json = JsonDocument.Parse(File.ReadAllText(full));
        return json.RootElement.TryGetProperty("scripts", out var scripts)
            ? scripts.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>();
    }

    /// <summary>The line up to an unquoted "#" that starts a word.</summary>
    private static string WithoutComment(string line)
    {
        var quote = '\0';
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (quote != '\0')
            {
                quote = c == quote ? '\0' : quote;
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '#' && (i == 0 || char.IsWhiteSpace(line[i - 1])))
            {
                return line[..i];
            }
        }

        return line;
    }

    /// <summary>The text cut at every unquoted separator, empty parts left out.</summary>
    private static IEnumerable<string> Split(string text, string[] separators)
    {
        var part = new StringBuilder();
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote == '\0' && separators.FirstOrDefault(s => string.CompareOrdinal(text, i, s, 0, s.Length) == 0) is { } separator)
            {
                if (part.ToString().Trim() is { Length: > 0 } done)
                {
                    yield return done;
                }

                part.Clear();
                i += separator.Length - 1;
                continue;
            }

            quote = quote == '\0' ? (c is '"' or '\'' ? c : '\0') : (c == quote ? '\0' : quote);
            part.Append(c);
        }

        if (part.ToString().Trim() is { Length: > 0 } last)
        {
            yield return last;
        }
    }
}
