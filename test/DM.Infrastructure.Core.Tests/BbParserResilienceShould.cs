using System;
using System.Collections.Generic;
using AwesomeAssertions;
using BBCodeParser;
using BBCodeParser.Nodes;
using BBCodeParser.Tags;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// What the renderer does with text meant to break it rather than to be read:
/// nesting deeper than a call stack, an attribute that never closes, a privacy
/// tag spelled so the parser stops seeing it.
/// </summary>
/// <remarks>Every case is reachable from the post form.</remarks>
public class BbParserResilienceShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid ReaderId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // Built by hand: the parser refuses this depth, and the walk is the point.
    [Fact]
    public void RenderTreeDeeperThanTheCallStack()
    {
        const int depth = 20000;
        var bold = new Tag("b", "<strong>", "</strong>");
        var tree = new NodeTree(BbParser.SecuritySubstitutions, new Dictionary<string, string>());

        Node current = tree;
        for (var level = 0; level < depth; level++)
        {
            var node = new TagNode(bold, current, null);
            current.AddChild(node);
            current = node;
        }

        current.AddChild(new TextNode("x"));

        tree.ToHtml().Should().HaveLength(depth * "<strong></strong>".Length + 1);
        tree.ToText().Should().Be("x");
        tree.ToBb().Should().HaveLength(depth * "[b][/b]".Length + 1);
    }

    [Fact]
    public void RefuseTextNestedDeeperThanTheDepthGuard()
    {
        var input = BbTestText.Repeat("[b]", 6000);
        input.Should().HaveLength(18000);

        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        var refuse = () => parser.Parse(input);

        refuse.Should().Throw<BbParserException>();
    }

    // The check asks the renderer rather than counting brackets, so it cannot drift from it.
    [Theory]
    [InlineData("[b]bold[/b] обычный текст", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("[b][b][b]x[/b][/b][/b]", true)]
    public void AgreeWithTheRendererOnTheSavePath(string? source, bool expected)
    {
        new BbCodeNestingLimit(_parserProvider).IsWithinLimit(source).Should().Be(expected);
    }

    [Fact]
    public void RefuseTheDeepInputOnTheSavePath()
    {
        new BbCodeNestingLimit(_parserProvider).IsWithinLimit(BbTestText.Repeat("[b]", 6000))
            .Should().BeFalse();
    }

    // Closed tags: a tag nobody closed renders as text.
    [Fact]
    public void RenderBothOutputsAtTheDeepestAcceptedNesting()
    {
        const int depth = 512;
        var input = BbTestText.Repeat("[b]", depth) + "x" + BbTestText.Repeat("[/b]", depth);
        var wrapper = _parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = DisplayContext(BbSurface.Comment);

        wrapper.RenderHtml(input, ctx).Should().EndWith("x" + BbTestText.Repeat("</strong>", depth));
        wrapper.RenderText(input, ctx).Should().Be("x");
    }

    [Fact]
    public void PrintTheDeepestAcceptedNestingWhenNothingClosesIt()
    {
        const int depth = 512;
        var input = BbTestText.Repeat("[b]", depth) + "x";
        var wrapper = _parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = DisplayContext(BbSurface.Comment);

        wrapper.RenderHtml(input, ctx).Should().Be(input);
        wrapper.RenderText(input, ctx).Should().Be(input);
    }

    // The whole chain: the balance check, the normaliser and the parser pattern each said yes on
    // its own.
    [Theory]
    [InlineData("[private=\"Гэн\nдальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\nдальф]тайна[/private]")]
    [InlineData("[private=\"Гэн[дальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\"дальф]тайна[/private]")]
    public void HidePrivateTextWhateverTheAttributeIsSpelled(string input)
    {
        var html = RenderForOutsider(input);

        html.Should().NotContain("тайна");
    }

    /// <summary>
    /// A block shown as written that is not one does not show the hidden block
    /// inside it: a [noparse] written with a value, and one nobody closed.
    /// </summary>
    /// <remarks>
    /// [noparse="x"] and [code="x"] are broken tags whose content is markup. Asserted on the page, on the
    /// plain text search is built from, and at the save path.
    /// </remarks>
    [Theory]
    [InlineData("[code=\"x\"][private=\"Вася\"]тайна[/private][/code]", true)]
    [InlineData("[noparse=\"x\"][private=\"Вася\"]тайна[/private][/noparse]", true)]
    [InlineData("[code=x][private]тайна[/private][/code]", true)]
    [InlineData("[code=\"x\"][private=\"Вася\"]тайна", false)]
    [InlineData("[noparse=\"x\"][private]тайна", false)]
    [InlineData("[noparse]пример [private=\"Вася\"]тайна", false)]
    [InlineData("[noparse]пример [private]тайна[/private] и хвост", false)]
    public void HidePrivateTextUnderACodeBlockThatIsNotOne(string input, bool accepted)
    {
        RenderForOutsider(input).Should().NotContain("тайна");

        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
        wrapper.RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost })
            .Should().NotContain("тайна");

        PrivateBlockMarkup.IsBalanced(input).Should().Be(accepted);
    }

    // A [noparse] is an example only when its own closing tag ends it; [code] only changes the
    // font.
    [Theory]
    [InlineData("[code]a -- b", "[code]a &ndash; b")]
    [InlineData("раз [noparse]a -- [b]b[/b]", "раз [noparse]a &ndash; <strong>b</strong>")]
    [InlineData("[code]a[noparse][b]b[/b][/noparse]", "[code]a[b]b[/b]")]
    public void PrintAnUnclosedCodeBlockAsTypedAndReadWhatFollowsIt(string input, string expected)
    {
        RenderComment(input).Should().Be(expected);
    }

    /// <summary>
    /// The save path refuses every one of those spellings and says how to write
    /// the block, and the render path still holds on its own for text that never
    /// went through it.
    /// </summary>
    /// <remarks>The balance check reads the block with a pattern that does not cross a line break.</remarks>
    [Theory]
    [InlineData("[private=\"Гэн\nдальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\nдальф]тайна[/private]")]
    [InlineData("[private=\"Гэн[дальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\"дальф]тайна[/private]")]
    public void RefuseTheMisspelledBlockAtTheSavePath(string input)
    {
        PrivateBlockMarkup.IsBalanced(input).Should().BeFalse();
        PrivateBlockMarkup.DescribeBalanceRefusal(input)
            .Should().EndWith("записан неверно, скрытый блок пишется [private=\"Имя\"] или [private].");
    }

    // The ordinary spelling still reaches the reader it names, so the guard above is not simply
    // hiding everything.
    [Fact]
    public void ShowPrivateTextToTheAddressee()
    {
        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
        var ctx = DisplayContext(BbSurface.GamePost) with
        {
            Viewer = Viewer(ReaderId),
            PostAuthorUserId = BbVisibilityRender.AuthorId,
            PrivateAddresseeOwnerUserIdsByAttribute = new Dictionary<string, IReadOnlySet<Guid>>(
                StringComparer.Ordinal)
            {
                ["Гэндальф"] = new HashSet<Guid> { ReaderId }
            }
        };

        var html = wrapper.RenderHtml("[private=\"Гэндальф\"]тайна[/private]", ctx);

        html.Should().Contain("тайна");
    }

    // Cost is asserted separately, in BbParserCostShould: those tests time the
    // renderer and have to run alone to mean anything.

    /// <summary>
    /// The DM2 attribute is written in quotes, and an unquoted one makes the tag
    /// a broken tag: the opening and the closing tag show as they were typed.
    /// </summary>
    /// <remarks>Neither DM2 nor the grammar has the unquoted form, and reading it means guessing where it ends.</remarks>
    [Fact]
    public void ShowTheUnquotedAttributeAsText()
    {
        RenderWithAttributeTag("[note=Вася]привет[/note]").Should().Be("[note=Вася]привет[/note]");
    }

    [Fact]
    public void EncodeTheQuotedAttribute()
    {
        var html = RenderWithAttributeTag("[note=\"<img src=x onerror=alert(1)>\"]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("note-head\">&lt;img src=x onerror=alert(1)&gt;</div>");
    }

    [Fact]
    public void EncodeTheUnquotedAttribute()
    {
        var html = RenderWithAttributeTag("[note=<img src=x onerror=alert(1)>]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    // There is one reading of the text, so the value is not read and the tag inside it is a tag.
    [Theory]
    [InlineData("[note=<img src=x onerror=alert(1)>[img]https://example.com/a.png[/img]]hi[/note]")]
    [InlineData("[note=\"<img src=x onerror=alert(1)>[img]https://example.com/a.png[/img]\"]hi[/note]")]
    [InlineData("[note=<img src=x onerror=alert(1)>[link text=\"t\"]https://example.com[/link]]hi[/note]")]
    [InlineData("[note=<img src=x onerror=alert(1)>[mention]Вася[/mention]]hi[/note]")]
    public void EndTheValueAtAnOpeningBracket(string input)
    {
        var html = RenderWithAttributeTag(input);

        html.Should().NotContain("<img src=x");
        html.Should().NotContain("note-head");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// A code block inside a value is a code block after the value, never part
    /// of it.
    /// </summary>
    [Fact]
    public void RefuseTheAttributeWhenACodeBlockStandsInIt()
    {
        var html = RenderWithAttributeTag("[note=<img src=x onerror=alert(1)>[code]x[/code]]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().NotContain("note-head");
    }

    // The address guards run where an image or a link renders, and these forms never render as
    // either.
    [Theory]
    [InlineData("[link=vbscript:msgbox(1)]click")]
    [InlineData("[link=\"vbscript:msgbox(1)\"]click")]
    [InlineData("[link=http://169.254.169.254/latest/meta-data]metadata")]
    [InlineData("[link=http://127.0.0.1:9200/_all]probe")]
    [InlineData("[img=200]")]
    [InlineData("[img]https://example.com/a.png")]
    public void LeaveAnUnacceptedImageOrLinkAsText(string input)
    {
        var html = RenderComment(input);

        html.Should().NotContain("<a href");
        html.Should().NotContain("<img");
        html.Should().Contain(input[..(input.IndexOf(']') + 1)]);
    }

    // A tag that has no attribute does not acquire one, so text already stored renders the way it
    // always did.
    [Theory]
    [InlineData("[b=1]жирный[/b]")]
    [InlineData("[spoiler=Заголовок]текст[/spoiler]")]
    public void LeaveTheUnquotedAttributeAloneOnATagThatHasNone(string input)
    {
        RenderComment(input).Should().Be(input);
    }

    // Control characters do not survive the parse, so no consumer downstream has to decide what to
    // do about them.
    [Theory]
    [InlineData(0x00)]
    [InlineData(0x08)]
    [InlineData(0x1B)]
    [InlineData(0x7F)]
    [InlineData(0x9D)]
    public void StripAControlCharacterFromTheInput(int code)
    {
        var html = RenderComment("a" + Character(code) + "b");

        html.Should().Be("ab");
    }

    [Fact]
    public void KeepTheControlCharactersThatMeanSomethingInText()
    {
        var html = RenderComment("a\tb\nc");

        html.Should().Be("a\tb<br />c");
    }

    // A surrogate with no partner is not a character; it becomes the one that says so, once, here,
    // instead of once per layer below.
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void ReplaceALoneSurrogate(int code)
    {
        var html = RenderComment("a" + Character(code) + "b");

        html.Should().Be("a" + Character(0xFFFD) + "b");
    }

    [Fact]
    public void KeepAPairedSurrogate()
    {
        var html = RenderComment("a\U0001F600b");

        html.Should().Be("a\U0001F600b");
    }

    // Marks that only nudge a neighbour's direction reorder nothing and stay.
    [Theory]
    [InlineData(0x202A)]
    [InlineData(0x202B)]
    [InlineData(0x202C)]
    [InlineData(0x202D)]
    [InlineData(0x202E)]
    [InlineData(0x2066)]
    [InlineData(0x2067)]
    [InlineData(0x2068)]
    [InlineData(0x2069)]
    public void StripTheCharactersThatReverseTheText(int code)
    {
        RenderComment("a" + Character(code) + "b").Should().Be("ab");
        RenderComment("[b" + Character(code) + "]x[/b]").Should().Be("<strong>x</strong>");

        var marks = Character(0x200E) + "a" + Character(0x200F);
        RenderComment(Character(code) + marks).Should().Be(marks);
    }

    [Fact]
    public void RenderATagInTextCarryingAControlCharacter()
    {
        var html = RenderComment("a" + Character(0x00) + "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img");
        html.Should().Contain("https://example.com/a.png");
    }

    // The character with this code point, written by number: the ones under test read as nothing.
    private static string Character(int code) => ((char)code).ToString();


    private string RenderComment(string input)
    {
        var wrapper = _parserProvider.GetForSurface(BbSurface.Comment);
        return wrapper.RenderHtml(input, DisplayContext(BbSurface.Comment));
    }

    private static string RenderWithAttributeTag(string input) =>
        BbAttributeTagParser.Wrapper.RenderHtml(input, DisplayContext(BbSurface.Comment));

    private string RenderForOutsider(string input)
    {
        var wrapper = _parserProvider.GetForSurface(BbSurface.GamePost);
        var ctx = DisplayContext(BbSurface.GamePost) with
        {
            Viewer = Viewer(ReaderId),
            PostAuthorUserId = BbVisibilityRender.AuthorId
        };
        return wrapper.RenderHtml(input, ctx);
    }

    private static RenderContext DisplayContext(BbSurface surface) => new()
    {
        Audience = RenderAudience.Display,
        Surface = surface,
        Viewer = Viewer(ReaderId)
    };

    private static IAuthorizationSubject Viewer(Guid userId) => BbVisibilityRender.Viewer(UserRole.RegularUser, userId);

}
