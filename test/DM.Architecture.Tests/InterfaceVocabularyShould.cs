using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One meaning is called by one word, and the letters of that word are the ones
/// the convention allows -- outside the client, where the client's own check
/// cannot see, and for the words in the client as well.
/// </summary>
/// <remarks>
/// GLOSSARY states the rule in as many words: one Russian word and one English
/// identifier per term, the same in the UI, in the code and in the
/// documentation. The client enforces it for interface strings
/// (copy-rules.spec.ts). Nothing enforced it anywhere else, and the gap showed:
/// the role every screen calls a "наставник" was still a "ментор" in five documents
/// -- including the glossary that declares the rule -- and in a seeded profile,
/// and the documentation is where the next screen takes its words from.
///
/// The two characters are here for the same reason. CODE_STYLE keeps the letter
/// at U+0451 and the typographic quotes out of every text a person reads, seeded
/// data and documentation included, and said in as many words that the rule was
/// checked by hand.
///
/// The check is textual because the offending value is prose: no type stands
/// between a word and the file it is written in. The retired words are a list
/// rather than a rule, exactly as on the client: neither of them is something
/// the code could re-derive.
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
    /// The forum entity is a "топик". The word "тема" means other things here --
    /// the colour theme, the subject of a ticket, the subject of a letter -- so
    /// the context decides and not the path, exactly as on the client
    /// (copy-rules.spec.ts).
    /// </summary>
    /// <remarks>
    /// The rule used to read "check the files whose path names the forum", which
    /// is a rule about where the word was last caught rather than about the word.
    /// The dictionary of notification headings
    /// (DM.Domain.Personal/Features/Notifications/NotificationWording.cs) is
    /// named after neither a topic nor a forum, so the line a reader sees over
    /// every forum notification was outside it; so were four passages of
    /// AUTHORIZATION.md that call the entity by the retired word.
    ///
    /// The ending is optional: the genitive plural has none. Without it the rule
    /// read "список закрепленных тем" and "удаление тем форума" as clean, and
    /// that is the form a list of topics is named in. The same three letters are
    /// also the pronoun ("тем же", "тем, что"), which <see cref="Pronoun"/> tells
    /// apart.
    /// </remarks>
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
    /// A word right before "тема" that makes it the entity wherever it stands.
    /// Only a topic is pinned, and the label of the pin handle
    /// ("Порядок закрепленной темы ...") has neither the forum nor a comment
    /// within reach in its template.
    /// </summary>
    private static readonly Regex EntityBefore =
        new(@"закрепл\p{L}*\s+$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>How far before the word <see cref="EntityBefore"/> looks, in characters.</summary>
    private const int Lead = 30;

    /// <summary>Text around the word that counts as its neighbourhood, in characters.</summary>
    private const int Neighbourhood = 60;

    /// <summary>
    /// The senses the word keeps: the subject of a conversation, of a letter, of
    /// a ticket, and the colour scheme. Listed because they do stand next to the
    /// forum -- the rules speak of "уход от темы в служебных разделах форума",
    /// which is about staying on subject and not about a "топик".
    /// </summary>
    private static readonly Regex[] OtherSenses =
    [
        new(@"(уход от|не по|по|на эту|об этой) тем[аыеу]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"тем[аыеу] (письма|жалобы|обращения)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(темн|светл)\w* тем|тем[аыеу] (оформления|сайта)|(переключени|настро)\w* темы|между темами",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)
    ];

    /// <summary>
    /// Names, not terms. The achievement for a twenty-fifth topic is called
    /// "Занятные темы" -- a title the owner wrote, and award titles are his to
    /// write (a neighbouring one is called "Народное признание, например").
    /// Renaming it would also be a seed change, and the seed is the same row in
    /// the migration and in both model snapshots. "Платиновые темы" is an item of
    /// the old site's lore (ERROR_PAGES_AND_LORE.md), listed under the name its
    /// community gave it.
    /// </summary>
    private static readonly Regex[] ProperNames =
    [
        new("Занятные темы", RegexOptions.Compiled),
        new("Платиновые темы", RegexOptions.Compiled)
    ];

    /// <summary>
    /// The retired word named as retired. The glossary's rows read "term, our
    /// word, the word we do not use", and the row for the forum entity has to
    /// spell the word it retires or it records nothing. Only that spelling is
    /// let through -- the word in quotes in the cell right after "Топик" -- so
    /// prose elsewhere in the same file that calls a topic by the old word is
    /// still caught.
    /// </summary>
    private static readonly Regex[] RetiredMentions =
    [
        new(@"\|\s*Топик\s*\|\s*""Тем[аыеу]""", RegexOptions.Compiled)
    ];

    /// <summary>The entity called by the other word, judged by the text around each hit.</summary>
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
        var root = RepositoryRoot;
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
        var root = RepositoryRoot;
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
    /// Every file outside the client that a person reads Russian in: the
    /// documentation, the agent instructions and the server sources. The client
    /// is left out because it checks itself, and the audit reports at the root
    /// are left out because they quote the violations they name.
    /// </summary>
    /// <remarks>
    /// The documents are every markdown file a commit carries, asked of git: a
    /// walk of README, docs and .claude left out the guides that live beside the
    /// scripts they describe (scripts/visual/README.md), and the other gates over
    /// hand-written documents (DocumentPunctuationShould and its neighbours) read
    /// the same set.
    /// </remarks>
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

        foreach (var file in FilesUnder(Path.Combine(root, "src"), "*.cs"))
        {
            yield return file;
        }
    }

    /// <summary>
    /// The client's own sources, for the words only. The client checks them too
    /// (copy-rules.spec.ts), but its rule for the entity stood where this one did
    /// and missed the same forms: the pin handle of every pinned topic was read
    /// out as "Порядок закрепленной темы". The characters stay the client's
    /// business: its motto is the one place the typographic quotes are allowed.
    /// Specs are left out, as there: they quote the words they forbid.
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
            foreach (var file in FilesUnder(client, pattern))
            {
                if (!file.EndsWith(".spec.ts", StringComparison.Ordinal) &&
                    !file.EndsWith(".d.ts", StringComparison.Ordinal))
                {
                    yield return file;
                }
            }
        }
    }

    private static IEnumerable<string> FilesUnder(string directory, string pattern)
    {
        foreach (var file in Directory.EnumerateFiles(directory, pattern))
        {
            yield return file;
        }

        foreach (var nested in Directory.EnumerateDirectories(directory))
        {
            if (Array.IndexOf(NotSource, Path.GetFileName(nested)) >= 0)
            {
                continue;
            }

            foreach (var file in FilesUnder(nested, pattern))
            {
                yield return file;
            }
        }
    }

    private static string Relative(string root, string file) =>
        Path.GetRelativePath(root, file).Replace('\\', '/');

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
