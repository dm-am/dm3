using System;
using AwesomeAssertions;
using BBCodeParser;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using DM.Testing.Markup;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// The save path writes the names of the tags of a text in lower case, as the
/// site writes every name, and changes nothing else.
/// </summary>
/// <remarks>
/// The rule is BBCODE_RENDERING.md, section "Регистр имен". Only what the
/// grammar reads as markup has its names written in lower case; text that only
/// looks like a tag keeps its case, and the page shows the text as it did.
/// </remarks>
public class BbTagNameCaseShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly IBbParserProvider Provider = new BbParserProvider();

    private readonly IBbTagNameCase _tagNames = new BbTagNameCase(Provider);

    [Theory]
    [InlineData("[B]текст[/B]", "[b]текст[/b]")]
    [InlineData("[B]текст[/b] и [b]текст[/B]", "[b]текст[/b] и [b]текст[/b]")]
    [InlineData("[SPOILER TEXT=\"Карта\"]текст[/SPOILER]", "[spoiler text=\"Карта\"]текст[/spoiler]")]
    [InlineData("[Spoiler  Text=\"Он сказал \"ДА\" И УШЕЛ\"]x[/Spoiler]",
        "[spoiler  text=\"Он сказал \"ДА\" И УШЕЛ\"]x[/spoiler]")]
    [InlineData("[IMG WIDTH=\"300\" Height=\"200\"]https://EXAMPLE.com/A.png[/IMG]",
        "[img width=\"300\" height=\"200\"]https://EXAMPLE.com/A.png[/img]")]
    [InlineData("[LINK TEXT=\"Главная\"]https://Example.com/[/LINK]",
        "[link text=\"Главная\"]https://Example.com/[/link]")]
    [InlineData("[PRIVATE=\"Вася, ПЕТЯ\"]секрет[/PRIVATE]", "[private=\"Вася, ПЕТЯ\"]секрет[/private]")]
    [InlineData("[Private]секрет[/PRIVATE]", "[private]секрет[/private]")]
    [InlineData("раз[TAB]два", "раз[tab]два")]
    [InlineData("[MENTION]Vasya[/MENTION]", "[mention]Vasya[/mention]")]
    [InlineData("[UL][LI]раз[/LI][/UL]", "[ul][li]раз[/li][/ul]")]
    [InlineData("[CODE][B]раз[/B][/CODE]", "[code][b]раз[/b][/code]")]
    [InlineData("[NOPARSE][B]пример[/B][/NOPARSE]", "[noparse][B]пример[/B][/noparse]")]
    public void WriteTheNamesOfMarkupInLowerCase(string typed, string saved) =>
        _tagNames.ToLowerCase(typed, BbTagSet.Common).Should().Be(saved);

    /// <summary>
    /// What the grammar does not read as markup is text, and text keeps its case:
    /// a broken tag, a tag left open, a closing tag with nothing to close, a
    /// content tag holding no value it accepts, a word in brackets.
    /// </summary>
    [Theory]
    [InlineData("[B x=\"1\"]текст[/B]")]
    [InlineData("[B=1]текст[/B]")]
    [InlineData("[B]текст без конца")]
    [InlineData("закрывается так: [/B]")]
    [InlineData("[LINK]не адрес[/LINK]")]
    [InlineData("[IMG ALT=\"кот\"]https://example.com/a.png[/IMG]")]
    [InlineData("[NOPARSE]пример без конца")]
    [InlineData("[S]зачеркнуто?[/S] [CUT] [COLOR=red]")]
    [InlineData("[PRIVATE=Вася]секрет[/PRIVATE]")]
    [InlineData("[PRIVATE]секрет")]
    [InlineData("ПРОСТО ТЕКСТ БЕЗ ТЕГОВ")]
    public void LeaveTextThatIsNoMarkupAsTyped(string typed) =>
        _tagNames.ToLowerCase(typed, BbTagSet.Common).Should().BeSameAs(typed);

    [Fact]
    public void WriteTheModBlockInLowerCase_WhereItIsATag()
    {
        _tagNames.ToLowerCase("[MOD]текст[/MOD]", BbTagSet.WithModBlock).Should().Be("[mod]текст[/mod]");
        _tagNames.ToLowerCase("[MOD]текст[/MOD]", BbTagSet.Common).Should().Be("[MOD]текст[/MOD]");
    }

    /// <summary>
    /// The description of a game tag is read with the hint's own tags: [tip] is
    /// markup there, and the tags of a post are text.
    /// </summary>
    [Theory]
    [InlineData("[TIP TEXT=\"Подсказка\"]СЛОВО[/TIP]", "[tip text=\"Подсказка\"]СЛОВО[/tip]")]
    [InlineData("[Tip IMG=\"/images/A.gif\" Text=\"x\"]слово[/TIP]", "[tip img=\"/images/A.gif\" text=\"x\"]слово[/tip]")]
    [InlineData("[NOPARSE][TIP]пример[/TIP][/NOPARSE]", "[noparse][TIP]пример[/TIP][/noparse]")]
    public void WriteTheNamesOfTheHintInLowerCase(string typed, string saved) =>
        _tagNames.ToLowerCase(typed, BbTagSet.Hint).Should().Be(saved);

    /// <summary>
    /// A [tip] the grammar reads as a node and the page still prints as typed,
    /// because it shows nothing or its address is refused, is text like any
    /// tag printed as typed: written in lower case, its brackets on the page
    /// would change. The tags of a post are text on the hint.
    /// </summary>
    [Theory]
    [InlineData("[TIP]слово[/TIP]")]
    [InlineData("[TIP TEXT=\"\"]слово[/TIP]")]
    [InlineData("[TIP IMG=\"images/a.gif\"]слово[/TIP]")]
    [InlineData("[B]текст[/B] [MOD]текст[/MOD]")]
    public void LeaveAHintThatIsNoMarkupAsTyped(string typed) =>
        _tagNames.ToLowerCase(typed, BbTagSet.Hint).Should().BeSameAs(typed);

    [Fact]
    public void LeaveATextWithNothingToChange_AsTheSameInstance()
    {
        const string typed = "[b]текст[/b] и Вася";

        _tagNames.ToLowerCase(typed, BbTagSet.Common).Should().BeSameAs(typed);
    }

    [Fact]
    public void LeaveATextTooDeepToParse_AsTyped()
    {
        var typed = string.Concat(System.Linq.Enumerable.Repeat("[B]", 600)) + "x";

        _tagNames.ToLowerCase(typed, BbTagSet.Common).Should().BeSameAs(typed);
    }

    /// <summary>
    /// On generated texts: the text saved differs from the text typed in the
    /// case of A-Z alone, writing it again changes nothing, and every reader,
    /// the author's editor, the plain text and the save path answer it as they
    /// answered the text typed.
    /// </summary>
    /// <remarks>
    /// The names are written in lower case after the save path took the text, so
    /// a surface is asked about a text only when its save path takes it: hiding
    /// markup off the game post, which the author's editor there gets back as
    /// text, never reaches this step.
    /// </remarks>
    [Fact]
    public void ChangeNothingButTheCaseOfNames_AndNothingTheSecondTime() =>
        MarkupProperty.Check(nameof(ChangeNothingButTheCaseOfNames_AndNothingTheSecondTime), runs: 300,
            random => random.Next(4) == 0
                ? MarkupGenerator.Valid(random, random.Pick(MarkupSurfaces.Content))
                : MarkupGenerator.Soup(random),
            input =>
            {
                var read = MarkupText.AsBothSidesRead(input);

                foreach (var tags in Enum.GetValues<BbTagSet>())
                {
                    var saved = _tagNames.ToLowerCase(read, tags);
                    if (!string.Equals(saved, read, StringComparison.OrdinalIgnoreCase) ||
                        saved.Length != read.Length)
                        return $"{tags}: more than the case changed: {MarkupLiteral.Of(saved)}";

                    var again = _tagNames.ToLowerCase(saved, tags);
                    if (again != saved)
                        return $"{tags}: a second pass changed {MarkupLiteral.Of(saved)} to {MarkupLiteral.Of(again)}";

                    if (PrivateBlockMarkup.IsBalanced(saved) != PrivateBlockMarkup.IsBalanced(read) ||
                        PrivateBlockMarkup.ContainsPrivateMarkup(saved) != PrivateBlockMarkup.ContainsPrivateMarkup(read))
                        return $"{tags}: the save path answers {MarkupLiteral.Of(saved)} otherwise";

                    foreach (var surface in SurfacesOf(tags))
                    {
                        var refused = surface == BbSurface.GamePost
                            ? !PrivateBlockMarkup.IsBalanced(read)
                            : PrivateBlockMarkup.ContainsPrivateMarkup(read);
                        if (refused) continue;

                        var shown = Answers(read, surface);
                        var shownSaved = Answers(saved, surface);
                        if (shown != shownSaved)
                            return $"{tags} on {surface}: the text is shown otherwise: {shown} against {shownSaved}";
                    }
                }

                return null;
            });

    private static BbSurface[] SurfacesOf(BbTagSet tags) => tags switch
    {
        BbTagSet.WithModBlock => [BbSurface.Comment, BbSurface.GlobalChatMessage],
        BbTagSet.Hint => [BbSurface.Hint],
        _ => [BbSurface.GamePost, BbSurface.Profile, BbSurface.DirectMessage]
    };

    /// <summary>What the author, a stranger, the author's editor and the plain text get.</summary>
    private static string Answers(string input, BbSurface surface)
    {
        try
        {
            var display = (BbParserWrapper)Provider.GetForSurface(surface);
            var edit = (BbParserWrapper)Provider.GetForAuthorEdit(surface);
            var author = Reader(AuthorId);
            return string.Join("\n---\n",
                display.RenderHtml(input, Context(surface, author)),
                display.RenderHtml(input, Context(surface, Reader(StrangerId))),
                edit.RenderHtml(input, RenderContext.ForAuthorEdit(author, surface)),
                display.RenderText(input, RenderContext.ForPlainText() with { Surface = surface }));
        }
        catch (BbParserException)
        {
            return "(too deep)";
        }
    }

    private static RenderContext Context(BbSurface surface, IAuthorizationSubject viewer) => new()
    {
        Audience = RenderAudience.Display,
        Surface = surface,
        Viewer = viewer,
        PostAuthorUserId = AuthorId
    };

    private static IAuthorizationSubject Reader(Guid id) => new TestSubject
    {
        UserId = id,
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };
}
