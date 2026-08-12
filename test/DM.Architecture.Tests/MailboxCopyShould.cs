using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The mailbox field is called "Почта" in every text a person reads.
/// </summary>
/// <remarks>
/// The login form labelled the field "Почта" and the server answered under it
/// with "Email обязателен": one field named two ways on one screen, across two
/// layers that nobody diffs against each other. A plain grep cannot hold the
/// rule, because the word stays legal as the name of a channel and as the
/// prefix of every Email* type in the code. So the scan looks only at what a
/// person can see — a string literal written in Russian, and the text a
/// template renders between its tags — and lets the channel naming through by
/// name.
/// </remarks>
public class MailboxCopyShould
{
    /// <summary>The word standing alone, not the Email* of an identifier.</summary>
    private static readonly Regex EmailWord = new(
        @"(?<![A-Za-z0-9_@.\-])email(?![A-Za-z0-9_@.\-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Cyrillic = new("[а-яА-Я]", RegexOptions.Compiled);

    /// <summary>
    /// C# literals, raw and verbatim ones first: """…""" and @"…" read as a run
    /// of empty pairs otherwise, and one mail body is a verbatim string.
    /// </summary>
    private static readonly Regex ServerLiterals = new(
        @"""{3,}[\s\S]*?""{3,}|@""(?:[^""]|"""")*""|""(?:[^""\\\n]|\\.)*""",
        RegexOptions.Compiled);

    /// <summary>The three delimiters the client writes strings with.</summary>
    private static readonly Regex ClientLiterals = new(
        @"""(?:[^""\\\n]|\\.)*""|'(?:[^'\\\n]|\\.)*'|`(?:[^`\\]|\\.)*`",
        RegexOptions.Compiled);

    /// <summary>Text between tags: copy a template shows without quoting it.</summary>
    private static readonly Regex TextNodes = new(">([^<>]*)<", RegexOptions.Compiled);

    /// <summary>An interpolation is code: {{ email }} renders an address, not the word.</summary>
    private static readonly Regex Interpolations = new(
        @"\{\{[^}]*\}\}|@\w[\w.]*", RegexOptions.Compiled);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// The one place the word names the channel and not our field: a signed-in
    /// author may be answered by mail or in Discord, and the hint lists both.
    /// </summary>
    private static readonly string[] ChannelNaming = ["Email или Discord"];

    private static readonly string[] ScannedExtensions = [".cs", ".vue", ".ts", ".razor"];

    private static readonly string[] BuildOutput =
        ["bin", "obj", "node_modules", "coverage", "dist", ".vite"];

    private static DirectoryInfo RepositoryRoot => DM.Testing.RepositoryLayout.RootDirectory;

    [Fact]
    public void CallTheFieldPochtaInEveryVisibleString()
    {
        var offenders = new List<string>();
        var scanned = 0;
        var russianCopy = 0;

        foreach (var file in SourceFiles(Path.Combine(RepositoryRoot.FullName, "src")))
        {
            scanned++;
            var text = File.ReadAllText(file);
            var extension = Path.GetExtension(file);
            var literals = extension == ".cs" ? ServerLiterals : ClientLiterals;

            foreach (Match literal in literals.Matches(text))
            {
                if (!Cyrillic.IsMatch(literal.Value))
                {
                    continue;
                }

                russianCopy++;
                if (EmailWord.IsMatch(literal.Value) && !NamesTheChannel(literal.Value))
                {
                    offenders.Add(Describe(file, text, literal.Index, literal.Value));
                }
            }

            var (markup, offset) = Markup(text, extension);
            foreach (Match node in TextNodes.Matches(markup))
            {
                var visible = Interpolations.Replace(node.Groups[1].Value, " ");
                if (EmailWord.IsMatch(visible) && !NamesTheChannel(visible))
                {
                    offenders.Add(Describe(file, text, offset + node.Groups[1].Index, visible));
                }
            }
        }

        scanned.Should().BeGreaterThan(0, "the sources of every module live under src/");
        russianCopy.Should().BeGreaterThan(0,
            "the interface is written in Russian, so the scan has to be seeing its copy");
        offenders.Should().BeEmpty(
            "the field is labelled \"Почта\", and a message that calls it otherwise reads " +
            "as being about some other field");
    }

    private static bool NamesTheChannel(string chunk) =>
        ChannelNaming.Any(naming => chunk.Contains(naming, StringComparison.Ordinal));

    /// <summary>
    /// The part of a file whose text is rendered as it stands. For a component
    /// that is the template block only: a `&gt;` in a script body would open a
    /// "text node" running to the next unrelated `&lt;`.
    /// </summary>
    private static (string Markup, int Offset) Markup(string text, string extension)
    {
        if (extension == ".razor")
        {
            return (text, 0);
        }

        if (extension != ".vue")
        {
            return (string.Empty, 0);
        }

        var start = text.IndexOf("<template", StringComparison.Ordinal);
        var end = text.LastIndexOf("</template>", StringComparison.Ordinal);
        return start < 0 || end <= start ? (string.Empty, 0) : (text[start..end], start);
    }

    private static string Describe(string file, string text, int index, string chunk)
    {
        var line = text.Take(index).Count(character => character == '\n') + 1;
        var single = Whitespace.Replace(chunk.Trim(), " ");
        return Path.GetFileName(file) + ":" + line + " " +
               (single.Length <= 90 ? single : single[..90]);
    }

    private static IEnumerable<string> SourceFiles(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory))
        {
            if (ScannedExtensions.Contains(Path.GetExtension(file))
                && !file.EndsWith(".spec.ts", StringComparison.Ordinal)
                && !file.EndsWith(".d.ts", StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (BuildOutput.Contains(Path.GetFileName(nested)))
            {
                continue;
            }

            foreach (var file in SourceFiles(nested))
            {
                yield return file;
            }
        }
    }
}
