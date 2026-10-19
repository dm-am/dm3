using System;
using System.Collections.Generic;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;
using static DM.Infrastructure.Core.Tests.BbVisibilityRender;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Everything inside a hidden block is hidden, images and all: a reader the block
/// is not for gets nothing of what it holds, and what is left of the post is the
/// same whatever the block held.
/// </summary>
/// <remarks>
/// The block here holds every kind of content a post carries: lines of text, an
/// image, a link with a text of its own, a spoiler with a caption, a quote, a
/// mention, tags inside tags, an 18+ block, a list, monospace and line breaks,
/// and the two things that could end it early: a [/private] shown as an example
/// and closing tags of other names. Its readers get all of it. A reader it is
/// not for gets none of its words, addresses, names or tags, and no element left
/// empty where it stood: what is left is pinned for each place a block can stand
/// in, and it is the same for a block of one letter, for this one and for this
/// one fifty times over, written to two characters, to nobody, or in a spelling
/// the grammar does not read, closed or not. A remainder that changed with the
/// content, its length or its addressees would tell the reader something of
/// them. The rule is BBCODE_RENDERING.md, section
/// "Zero-information erase". BbMarkupPropertiesShould holds it on generated
/// texts, whose blocks seldom hold an image, a link and a spoiler at once.
/// </remarks>
public class BbPrivateBlockErasureShould
{
    /// <summary>The place of the block in a post.</summary>
    private const string Here = "BLOCK";

    private const string Content =
        "Ключ лежит под третьей доской.\n" +
        "[img]https://tainik.example.net/karta.png[/img]\n" +
        "[link text=\"Тайник\"]https://tainik.example.net/lodka[/link]\n" +
        "[spoiler text=\"Шифр\"]Север, два шага[/spoiler]\n" +
        "[quote]Никому ни слова[/quote]\n" +
        "[mention]Сторож[/mention] [b][i][u]Пароль сова[/u][/i][/b]\n" +
        "[nsfw]Кровь на полу[/nsfw]\n" +
        "[ul][li]Весло у причала[/li][/ul]\n" +
        "[noparse]пример [/private] тега[/noparse]\n" +
        "лишний [/quote] и [/b] закрывающий\n" +
        "[code]дверь 7[/code] и последняя строка";

    /// <summary>What the content puts on the page for its readers, one piece each.</summary>
    private static readonly string[] Pieces =
    [
        "Ключ лежит под третьей доской.", "<img src=\"https://tainik.example.net/karta.png\"",
        "href=\"https://tainik.example.net/lodka\"", ">Тайник</a>", "Шифр", "Север, два шага",
        "Никому ни слова", "@Сторож", "<strong><em><u>Пароль сова</u></em></strong>", "Кровь на полу",
        "<li>Весло у причала</li>", "пример [/private] тега", "лишний [/quote] и [/b] закрывающий", "дверь 7",
        "и последняя строка",
    ];

    /// <summary>
    /// What no reader the block is not for may find: its words, its addresses, the
    /// names it is written to, and any of its tags or of the element it is drawn as.
    /// </summary>
    private static readonly string[] Traces =
    [
        "Ключ", "доской", "tainik", "karta", "lodka", "Тайник", "Шифр", "Север", "Никому", "Сторож",
        "Пароль", "сова", "Кровь", "Весло", "пример", "лишний", "закрывающий", "дверь", "последняя", "Вася",
        "Петя", "x",
        "private", "recipients", "data-bb", "nsfw", "mention", "<img", "<code", "<strong", "[", "]",
    ];

    private static readonly (string Name, string Text)[] Contents =
    [
        ("one letter", "x"),
        ("everything", Content),
        ("everything fifty times", string.Join("\n", Enumerable.Repeat(Content, 50))),
    ];

    /// <summary>
    /// The ways a block is written, whether it reaches an addressee, and whom it
    /// names to its readers: a spelling the grammar does not read addresses nobody.
    /// </summary>
    private static readonly (string Name, string Opening, bool Addressed, string Recipients)[] Spellings =
    [
        ("written to two characters", "[private=\"Вася, Петя\"]", true, "DungeonMaster, Assistant, Вася, Петя"),
        ("written to them in capitals", "[PRIVATE=\"Вася, Петя\"]", true, "DungeonMaster, Assistant, Вася, Петя"),
        ("written to nobody", "[private]", false, "DungeonMaster, Assistant"),
        ("spelled without quotes", "[private=Вася]", false, "DungeonMaster, Assistant"),
        ("spelled with a quote left open", "[private=\"Вася]", false, "DungeonMaster, Assistant"),
        ("spelled with a parameter", "[private to=\"Вася\"]", false, "DungeonMaster, Assistant"),
    ];

    /// <summary>A post with the block in it, and what a reader the block is not for gets of it.</summary>
    private static readonly (string Post, string Remainder)[] Places =
    [
        ("Эльвира входит в каюту.\nBLOCK\nПотом она выходит на палубу.",
            "Эльвира входит в каюту.<br />Потом она выходит на палубу."),
        ("Эльвира входит BLOCK в каюту.", "Эльвира входит  в каюту."),
        ("BLOCK\nЭльвира входит в каюту.", "Эльвира входит в каюту."),
        ("Эльвира входит в каюту.\nBLOCK", "Эльвира входит в каюту."),
        ("BLOCK", ""),
        ("Эльвира [b]BLOCK[/b] входит.", "Эльвира  входит."),
        ("[spoiler text=\"Каюта\"]BLOCK[/spoiler]\nЭльвира входит.", "Эльвира входит."),
        ("[quote]Капитан сказал:\nBLOCK\nи ушел.[/quote]", "<div class=\"quote\">Капитан сказал:<br />и ушел.</div>"),
        ("[ul][li]Каюта[/li][li]BLOCK[/li][/ul]", "<ul><li>Каюта</li></ul>"),
        ("[link]https://example.com/BLOCK[/link]",
            "<a href=\"https://example.com/\" target=\"_blank\" rel=\"noopener\">ссылка</a>"),
    ];

    public static TheoryData<string, string> PostsAndRemainders()
    {
        var data = new TheoryData<string, string>();
        foreach (var (post, remainder) in Places)
        {
            data.Add(post, remainder);
        }

        return data;
    }

    public static TheoryData<string> Posts() => new(Places.Select(place => place.Post));

    /// <summary>
    /// A guest, another player, a mentor, a moderator and an administrator, none of
    /// them a lead of the game, get the same remainder of the post whatever the
    /// block held, however long it was and whomever it was written to.
    /// </summary>
    [Theory]
    [MemberData(nameof(PostsAndRemainders))]
    public void LeaveAReaderTheBlockIsNotForTheSameRemainder(string post, string remainder)
    {
        var problems = new List<string>();
        foreach (var spelling in Spellings)
        {
            foreach (var content in Contents)
            {
                foreach (var (reader, viewer) in Strangers(spelling.Addressed))
                {
                    var html = Display(post.Replace(Here, Block(spelling.Opening, content.Text)), viewer);
                    var where = $"a block {spelling.Name} holding {content.Name}, read by {reader}";
                    problems.AddRange(Leaks(html, remainder).Select(leak => $"{where}: {leak}"));
                }
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a reader the block is not for gets the post as if it held nothing, and the same post " +
            "whatever it held");
    }

    /// <summary>
    /// The plain text of the mail and the embed cut every block, and what they leave
    /// is the same whatever the block held.
    /// </summary>
    [Theory]
    [MemberData(nameof(Posts))]
    public void LeaveThePlainTextAndTheEmbedTheSameRemainder(string post)
    {
        var plainText = RenderText(post.Replace(Here, "[private]x[/private]"), BbSurface.GamePost,
            RenderAudience.PlainText);
        var embed = BbAudienceRender.EmbedSafe(BbSurface.GamePost, post.Replace(Here, "[private]x[/private]"));

        var problems = new List<string>();
        foreach (var spelling in Spellings)
        {
            foreach (var content in Contents)
            {
                var text = post.Replace(Here, Block(spelling.Opening, content.Text));
                var where = $"a block {spelling.Name} holding {content.Name}";
                problems.AddRange(Leaks(RenderText(text, BbSurface.GamePost, RenderAudience.PlainText), plainText)
                    .Select(leak => $"{where}, plain text: {leak}"));
                problems.AddRange(Leaks(BbAudienceRender.EmbedSafe(BbSurface.GamePost, text), embed)
                    .Select(leak => $"{where}, embed: {leak}"));
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "the mail and the embed are read by anybody, and a block leaves no more in them than on the page");
    }

    /// <summary>
    /// The author, the master and, for a block written to their character, the
    /// addressee get the whole of it, each piece drawn as it is drawn outside the
    /// block, inside the element that names who reads it.
    /// </summary>
    [Theory]
    [MemberData(nameof(Posts))]
    public void ShowTheReadersOfTheBlockAllOfIt(string post)
    {
        var drawn = Display(Content, Viewer(UserRole.RegularUser, AuthorId));
        Pieces.Where(piece => !drawn.Contains(piece, StringComparison.Ordinal)).Should().BeEmpty(
            "the content puts each of its pieces on the page, or the readers below are checked for nothing");

        foreach (var spelling in Spellings)
        {
            var whole = $"<span class=\"private-message\" data-recipients=\"{spelling.Recipients}\">{drawn}</span>";
            foreach (var (reader, viewer) in Readers(spelling.Addressed))
            {
                Display(post.Replace(Here, Block(spelling.Opening, Content)), viewer).Should().Contain(whole,
                    $"{reader} reads a block {spelling.Name} whole");
            }
        }
    }

    /// <summary>
    /// A block nobody closed runs to the end of the text: a reader it is not for
    /// gets nothing of what follows its opening tag, and its readers get all of it.
    /// </summary>
    [Fact]
    public void HideABlockNobodyClosedToTheEndOfTheText()
    {
        const string line = "Эльвира входит в каюту.";
        var drawn = Display(Content, Viewer(UserRole.RegularUser, AuthorId));

        var problems = new List<string>();
        foreach (var spelling in Spellings)
        {
            foreach (var content in Contents)
            {
                foreach (var (reader, viewer) in Strangers(spelling.Addressed))
                {
                    var where = $"an unclosed block {spelling.Name} holding {content.Name}, read by {reader}";
                    problems.AddRange(Leaks(Display(line + "\n" + spelling.Opening + content.Text, viewer), line)
                        .Select(leak => $"{where}: {leak}"));
                }
            }

            var whole = $"<span class=\"private-message\" data-recipients=\"{spelling.Recipients}\">{drawn}</span>";
            foreach (var (reader, viewer) in Readers(spelling.Addressed))
            {
                var html = Display(line + "\n" + spelling.Opening + Content, viewer);
                if (!html.Contains(whole, StringComparison.Ordinal))
                {
                    problems.Add($"{reader} does not read an unclosed block {spelling.Name} whole: {Shortened(html)}");
                }
            }
        }

        string.Join(Environment.NewLine, problems).Should().BeEmpty(
            "a block nobody closed is still a block, and it hides to the end of the text what it would hide closed");
    }

    private static string Block(string opening, string content) => opening + content + "[/private]";

    /// <summary>What the rendering gives that the remainder does not: a different text, and each trace in it.</summary>
    private static IEnumerable<string> Leaks(string html, string remainder)
    {
        if (!string.Equals(html, remainder, StringComparison.Ordinal))
        {
            yield return $"got \"{Shortened(html)}\" where the remainder is \"{remainder}\"";
        }

        foreach (var trace in Traces.Where(trace => html.Contains(trace, StringComparison.Ordinal) &&
                                                    !remainder.Contains(trace, StringComparison.Ordinal)))
        {
            yield return $"\"{trace}\" reached the reader";
        }
    }

    private static string Shortened(string html) => html.Length <= 300 ? html : html[..300] + " (cut)";

    private static string Display(string bbCode, IAuthorizationSubject viewer) =>
        RenderWithContext(bbCode, BbSurface.GamePost, DisplayCtxForGamePost(viewer) with
        {
            GameLeadUserIds = [MasterId],
            PrivateAddresseeOwnerUserIdsByAttribute = new Dictionary<string, IReadOnlySet<Guid>>(StringComparer.Ordinal)
            {
                ["Вася, Петя"] = new HashSet<Guid> { OwnerB },
            },
        });

    /// <summary>The readers a block is not for: nobody of them wrote it, leads the game or is named in it.</summary>
    private static IEnumerable<(string Name, IAuthorizationSubject Viewer)> Strangers(bool addressed)
    {
        yield return ("a guest", Viewer(UserRole.Guest));
        yield return ("another player", Viewer(UserRole.RegularUser, OtherUser));
        yield return ("a mentor", Viewer(UserRole.Mentor, OtherUser));
        yield return ("a moderator", Viewer(UserRole.Moderator, OtherUser));
        yield return ("an administrator", Viewer(UserRole.Admin, OtherUser));
        if (!addressed)
        {
            yield return ("the owner of the character the name stands for", Viewer(UserRole.RegularUser, OwnerB));
        }
    }

    private static IEnumerable<(string Name, IAuthorizationSubject Viewer)> Readers(bool addressed)
    {
        yield return ("the author", Viewer(UserRole.RegularUser, AuthorId));
        yield return ("the master", Viewer(UserRole.RegularUser, MasterId));
        if (addressed)
        {
            yield return ("the addressee", Viewer(UserRole.RegularUser, OwnerB));
        }
    }
}
