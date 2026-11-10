using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// GLOSSARY states the rule, and the client enforces it for its strings (copy-rules.spec.ts).
/// CODE_STYLE keeps U+0451 and the typographic quotes out of every text a person reads. Textual:
/// the offending value is prose.
/// </remarks>
public class InterfaceVocabularyShould
{
    /// <summary>
    /// Generated trees and foreign checkouts. A worktree is a second copy of
    /// this repository and may hold the text of any branch.
    /// </summary>
    private static readonly string[] NotSource =
        ["bin", "obj", "node_modules", "coverage", "dist", "worktrees"];

    /// <summary>
    /// A word the site retired, and the word it kept. The pattern opens on a
    /// word boundary: "старт конкурса" holds the letters of the second entry
    /// and is not it.
    /// </summary>
    private static readonly (Regex Pattern, string Retired, string Instead)[] RetiredWording =
    [
        (new Regex(@"\bментор", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "ментор",
            "наставник: the badge, the roles table, the game settings and GLOSSARY all say so"),
        (new Regex(@"\bарт конкурс", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "арт конкурс",
            "арт-конкурс, with the hyphen Russian puts there")
    ];

    /// <summary>
    /// The forum entity is a "топик"; "тема" means other things too, so the context decides, as on the
    /// client. The ending is optional (the genitive plural has none), and <see cref="Pronoun"/> tells
    /// the pronoun apart.
    /// </summary>
    private static readonly Regex Tema =
        new(@"(?<![А-Яа-я])[Тт]ем(а|ы|е|у|ой|ам|ах|ами)?(?![А-Яа-я])", RegexOptions.Compiled);

    /// <summary>
    /// The bare "тем" that is the pronoun and not the noun, read from the word
    /// on: "тем же", "тем не менее", "тем лучше", "тем ли способом", "под тем
    /// полем", "тем, кто". A line break counts as the space it stands for.
    /// </summary>
    private static readonly Regex Pronoun =
        new(@"^[Тт]ем(\s+(же|самым|не\s+менее|более|больше|лучше|хуже|меньше|сильнее|ли|что|кто|\p{L}+(ом|ем|ым|им))(?!\p{L})|\s*,\s*(что|кто|чем|как|куда|где|у|к|в|на|с)(?!\p{L}))",
            RegexOptions.Compiled);

    /// <summary>
    /// The forum standing next to the word is what makes it the entity, and so do
    /// comments: a "тема с чужими комментариями" or "пост, тема, комментарий" is
    /// the thing a comment is written under.
    /// </summary>
    private static readonly Regex ForumNearby =
        new("форум|топик|topic|комментари", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// A word right before "тема" that makes it the entity wherever it stands: only a topic is pinned.
    /// </summary>
    private static readonly Regex EntityBefore =
        new(@"закрепл\p{L}*\s+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int Lead = 30;

    private const int Neighbourhood = 60;

    private static readonly Regex[] OtherSenses =
    [
        new(@"(уход от|не по|по|на эту|об этой) тем[аыеу]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"тем[аыеу] (письма|жалобы|обращения)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(темн|светл)\w* тем|тем[аыеу] (оформления|сайта)|(переключени|настро)\w* темы|между темами",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    /// <summary>
    /// Names, not terms: award titles such as "Занятные темы", also seeded in the migration and snapshots.
    /// </summary>
    private static readonly Regex[] ProperNames =
    [
        new("Занятные темы", RegexOptions.Compiled)
    ];

    private static readonly Regex[] RetiredMentions =
    [
        new(@"\|\s*Топик\s*\|\s*""Тем[аыеу]""", RegexOptions.Compiled)
    ];

    private static bool CallsTheEntityATema(string text)
    {
        foreach (Match hit in Tema.Matches(text))
        {
            if (!hit.Groups[1].Success && Pronoun.IsMatch(text[hit.Index..Math.Min(text.Length, hit.Index + 40)]))
            {
                continue;
            }

            var from = Math.Max(0, hit.Index - Neighbourhood);
            var to = Math.Min(text.Length, hit.Index + hit.Length + Neighbourhood);
            var around = text[from..to];
            var lead = text[Math.Max(0, hit.Index - Lead)..hit.Index];

            if (!ForumNearby.IsMatch(around) && !EntityBefore.IsMatch(lead))
            {
                continue;
            }

            if (Array.Exists(OtherSenses, sense => sense.IsMatch(around)))
            {
                continue;
            }

            if (Array.Exists(ProperNames, name => name.IsMatch(around)))
            {
                continue;
            }

            if (Array.Exists(RetiredMentions, mention => mention.IsMatch(around)))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// The letter at U+0451 and its capital, by number: a file that keeps a
    /// character out of the text should not carry it.
    /// </summary>
    private static readonly char[] EWithDots = [(char)0x0451, (char)0x0401];

    /// <summary>The quotes CODE_STYLE reserves for the motto, by number as well.</summary>
    private static readonly char[] Guillemets = [(char)0x00AB, (char)0x00BB];

    /// <summary>
    /// The one file that has to hold the letter: it maps every Cyrillic letter
    /// to Latin, and a table missing one transliterates it to nothing.
    /// </summary>
    private const string TransliterationTable =
        "src/DM.Domain.Core/Extensions/ReadableGuidHelper.cs";

    [Fact]
    public void CallOneMeaningByOneWord()
    {
        var root = RepositoryLayout.Root;
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in ReadableText(root).Concat(ClientText(root)))
        {
            scanned++;
            var text = File.ReadAllText(file);

            var relative = Relative(root, file);

            foreach (var (pattern, retired, instead) in RetiredWording)
            {
                if (pattern.IsMatch(text))
                {
                    offenders.Add($"{relative}: \"{retired}\", use {instead}");
                }
            }

            if (CallsTheEntityATema(text))
            {
                offenders.Add($"{relative}: the forum entity is a \"топик\"");
            }
        }

        scanned.Should().BeGreaterThan(0, "a rule that reads nothing passes");
        offenders.Should().BeEmpty(
            "the documentation, the seed and the screens are where the next screen takes its words from");
    }

    [Fact]
    public void SpellRussianWithTheCharactersTheConventionAllows()
    {
        var root = RepositoryLayout.Root;
        var offenders = new List<string>();
        var scanned = 0;

        foreach (var file in ReadableText(root))
        {
            scanned++;
            var relative = Relative(root, file);
            var text = File.ReadAllText(file);

            if (relative != TransliterationTable && text.IndexOfAny(EWithDots) >= 0)
            {
                offenders.Add($"{relative}: the letter at U+0451, the site spells it without the dots");
            }

            if (text.IndexOfAny(Guillemets) >= 0)
            {
                offenders.Add($"{relative}: typographic quotes are reserved for the motto");
            }
        }

        scanned.Should().BeGreaterThan(0, "a rule that reads nothing passes");
        offenders.Should().BeEmpty(
            "the rules for Russian text hold for documentation and server sources as well");
    }

    /// <summary>
    /// Every file outside the client a person reads Russian in: every markdown file a commit carries and
    /// the server sources. The client checks itself; audit reports quote what they name.
    /// </summary>
    private static IEnumerable<string> ReadableText(string root)
    {
        foreach (var file in RepositoryFiles.CommittableFiles())
        {
            if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase) &&
                !Relative(root, file).StartsWith("AUDIT-", StringComparison.Ordinal))
            {
                yield return file;
            }
        }

        foreach (var file in RepositoryFiles.Files(Path.Combine(root, "src"), "*.cs", NotSource))
        {
            yield return file;
        }
    }

    /// <summary>
    /// The client's own sources, for the words only; specs are left out, they quote the words they forbid.
    /// </summary>
    private static IEnumerable<string> ClientText(string root)
    {
        var client = Path.Combine(root, "src", "DM.Web.Client", "src");
        if (!Directory.Exists(client))
        {
            yield break;
        }

        foreach (var pattern in new[] { "*.vue", "*.ts" })
        {
            foreach (var file in RepositoryFiles.Files(client, pattern, NotSource))
            {
                if (!file.EndsWith(".spec.ts", StringComparison.Ordinal) &&
                    !file.EndsWith(".d.ts", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');
}
