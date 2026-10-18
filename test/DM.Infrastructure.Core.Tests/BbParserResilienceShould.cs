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
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// What the renderer does with text meant to break it rather than to be read:
/// nesting deeper than a call stack, an attribute that never closes, a privacy
/// tag spelled so the parser stops seeing it.
/// </summary>
/// <remarks>
/// Every case here was reachable from the post form. Two of them were not
/// failures the reader would notice - one killed the process outright, the
/// other published the private line to the room - which is why they are asserted
/// rather than described.
/// </remarks>
public class BbParserResilienceShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ReaderId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ════════════════════════════════════════════════════════════════
    // Depth: the tree walk must not be the call stack
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// A tree nested deeper than the runtime stack can carry still renders.
    /// </summary>
    /// <remarks>
    /// Built by hand rather than parsed, on purpose: the parser refuses this
    /// depth (see RefuseTextNestedDeeperThanTheDepthGuard below), and the point
    /// here is the walk itself, not the guard in front of it. With the recursive
    /// renderer this test did not fail - it ended the test run, because a
    /// StackOverflowException in .NET is not raised, it is a process kill, and
    /// the depth below is five times what the default 1 MB stack carried.
    /// </remarks>
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

    /// <summary>
    /// The 18 KB of nested [b] that used to take the API process down is refused
    /// by the parser, and the refusal is an exception a caller can catch.
    /// </summary>
    /// <remarks>
    /// Not a single closing tag is needed: opening tags alone build the tree.
    /// The old depth guard stood at 6000 and this input reaches exactly 6000, so
    /// it passed the guard and died in the renderer instead - the guard was set
    /// to twice the depth the renderer survived and therefore never fired.
    /// </remarks>
    [Fact]
    public void RefuseTextNestedDeeperThanTheDepthGuard()
    {
        var input = BbTestText.Repeat("[b]", 6000);
        input.Should().HaveLength(18000);

        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        var refuse = () => parser.Parse(input);

        refuse.Should().Throw<BbParserException>();
    }

    /// <summary>
    /// The save path is told the same thing the render path would decide.
    /// </summary>
    /// <remarks>
    /// Without this the refusal was invisible from both ends: nothing on the way
    /// in looked, so the post was stored, and from then on it showed as an empty
    /// field to every reader but its author - no error, no marker - while the
    /// author's own view renders the source and looked fine to them. The check
    /// asks the renderer rather than counting brackets itself, which is why it
    /// cannot drift from it.
    /// </remarks>
    [Theory]
    [InlineData("[b]bold[/b] обычный текст", true)]
    [InlineData("", true)]
    [InlineData(null, true)]
    [InlineData("[b][b][b]x[/b][/b][/b]", true)]
    public void AgreeWithTheRenderer_OnTheSavePath(string? source, bool expected)
    {
        new BbCodeNestingLimit(_parserProvider).IsWithinLimit(source).Should().Be(expected);
    }

    /// <summary>The 18 KB that used to kill the process does not get saved.</summary>
    [Fact]
    public void RefuseTheDeepInput_OnTheSavePath()
    {
        new BbCodeNestingLimit(_parserProvider).IsWithinLimit(BbTestText.Repeat("[b]", 6000))
            .Should().BeFalse();
    }

    /// <summary>
    /// Both render paths, not just the HTML one, survive the deepest tree the
    /// parser will build.
    /// </summary>
    /// <remarks>
    /// Closed tags, because a tag nobody closed renders as the text it is, and
    /// the tree would then be printed rather than drawn.
    /// </remarks>
    [Fact]
    public void RenderBothOutputs_AtTheDeepestAcceptedNesting()
    {
        const int depth = 512;
        var input = BbTestText.Repeat("[b]", depth) + "x" + BbTestText.Repeat("[/b]", depth);
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = DisplayContext(BbSurface.Comment);

        wrapper.RenderHtml(input, ctx).Should().EndWith("x" + BbTestText.Repeat("</strong>", depth));
        wrapper.RenderText(input, ctx).Should().Be("x");
    }

    /// <summary>
    /// The same depth left unclosed is printed as it was typed, in both outputs.
    /// </summary>
    [Fact]
    public void PrintTheDeepestAcceptedNesting_WhenNothingClosesIt()
    {
        const int depth = 512;
        var input = BbTestText.Repeat("[b]", depth) + "x";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = DisplayContext(BbSurface.Comment);

        wrapper.RenderHtml(input, ctx).Should().Be(input);
        wrapper.RenderText(input, ctx).Should().Be(input);
    }

    // ════════════════════════════════════════════════════════════════
    // Privacy: a spelling the parser refuses must not become plain text
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// A private block whose addressee carries a line break is not shown to a
    /// reader it was not addressed to.
    /// </summary>
    /// <remarks>
    /// The whole chain, because every link of it said yes: the save-time balance
    /// check accepted the post, the normaliser passed the spelling through, and
    /// the parser's attribute pattern - a dot with no Singleline - refused to
    /// cross the line break and stopped seeing a tag at all. No tag, no node, no
    /// node for the visibility filter to remove, and the private line was
    /// rendered as ordinary text to every reader of the room. Asserting the
    /// pattern alone would have proved nothing: the pattern was doing what it
    /// said.
    /// </remarks>
    [Theory]
    [InlineData("[private=\"Гэн\nдальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\nдальф]тайна[/private]")]
    [InlineData("[private=\"Гэн[дальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\"дальф]тайна[/private]")]
    public void HidePrivateText_WhateverTheAttributeIsSpelled(string input)
    {
        var html = RenderForOutsider(input);

        html.Should().NotContain("тайна");
    }

    /// <summary>
    /// A block shown as written that is not one does not show the hidden block
    /// inside it: a [noparse] written with a value, and one nobody closed.
    /// </summary>
    /// <remarks>
    /// [noparse="x"] is a broken tag, printed as typed, and what it holds is read
    /// as markup like any other text: an example the page drew around a hidden
    /// block would print it to the room, and the search text would carry it the
    /// same way. [code="x"] is a broken tag as well, and holds the same way, though
    /// even a well spelled [code] shows nothing as an example. Asserted on the
    /// page, on the plain text the search is built from, and at the save path,
    /// which accepts the closed hidden block because on the page it is an
    /// ordinary one.
    /// </remarks>
    [Theory]
    [InlineData("[code=\"x\"][private=\"Вася\"]тайна[/private][/code]", true)]
    [InlineData("[noparse=\"x\"][private=\"Вася\"]тайна[/private][/noparse]", true)]
    [InlineData("[code=x][private]тайна[/private][/code]", true)]
    [InlineData("[code=\"x\"][private=\"Вася\"]тайна", false)]
    [InlineData("[noparse=\"x\"][private]тайна", false)]
    [InlineData("[noparse]пример [private=\"Вася\"]тайна", false)]
    [InlineData("[noparse]пример [private]тайна[/private] и хвост", false)]
    public void HidePrivateText_UnderACodeBlockThatIsNotOne(string input, bool accepted)
    {
        RenderForOutsider(input).Should().NotContain("тайна");

        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        wrapper.RenderText(input, RenderContext.ForPlainText() with { Surface = BbSurface.GamePost })
            .Should().NotContain("тайна");

        PrivateBlockMarkup.IsBalanced(input).Should().Be(accepted);
    }

    /// <summary>
    /// A [code] or a [noparse] nobody closed is the text it is, and what follows
    /// its opening is read like any other text.
    /// </summary>
    /// <remarks>
    /// Both used to be drawn to the end of the text unless a hidden block stood
    /// in them, in which case they turned into text after all: two outcomes for
    /// one mistake, decided by something further down the post. A [noparse] is
    /// an example only when its own closing tag says where the example ends, and
    /// a [code] is no example at all: it changes the font.
    /// </remarks>
    [Theory]
    [InlineData("[code]a -- b", "[code]a &ndash; b")]
    [InlineData("раз [noparse]a -- [b]b[/b]", "раз [noparse]a &ndash; <strong>b</strong>")]
    [InlineData("[code]a[noparse][b]b[/b][/noparse]", "[code]a[b]b[/b]")]
    public void PrintAnUnclosedCodeBlockAsTyped_AndReadWhatFollowsIt(string input, string expected)
    {
        RenderComment(input).Should().Be(expected);
    }

    /// <summary>
    /// The save path refuses every one of those spellings and says how to write
    /// the block, and the render path still holds on its own for text that never
    /// went through it.
    /// </summary>
    /// <remarks>
    /// It used to accept the line-break spelling: the balance check read the
    /// block with a pattern that crossed a line break, so from where it stood
    /// the post was well formed, and nothing between it and the page disagreed
    /// until the parser, which disagreed silently.
    /// </remarks>
    [Theory]
    [InlineData("[private=\"Гэн\nдальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\nдальф]тайна[/private]")]
    [InlineData("[private=\"Гэн[дальф\"]тайна[/private]")]
    [InlineData("[private=Гэн\"дальф]тайна[/private]")]
    public void RefuseTheMisspelledBlock_AtTheSavePath(string input)
    {
        PrivateBlockMarkup.IsBalanced(input).Should().BeFalse();
        PrivateBlockMarkup.DescribeBalanceRefusal(input)
            .Should().Contain("символ 1").And.Contain("[private=\"Имя\"]");
    }

    /// <summary>
    /// The ordinary spelling still reaches the reader it names, so the guard
    /// above is not simply hiding everything.
    /// </summary>
    [Fact]
    public void ShowPrivateText_ToTheAddressee()
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var ctx = DisplayContext(BbSurface.GamePost) with
        {
            Viewer = Viewer(ReaderId),
            PostAuthorUserId = AuthorId,
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

    // ════════════════════════════════════════════════════════════════
    // Attribute values
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// The DM2 attribute is written in quotes, and an unquoted one makes the tag
    /// a broken tag: the opening and the closing tag show as they were typed.
    /// </summary>
    /// <remarks>
    /// The unquoted form used to be read, first by nobody — the opening tag
    /// stayed on the page while the closing tag was consumed — and later by a
    /// pattern of its own. Neither DM2 nor the grammar has it, and reading a
    /// value without quotes means guessing where it ends.
    /// </remarks>
    [Fact]
    public void ShowTheUnquotedAttributeAsText()
    {
        RenderWithAttributeTag("[note=Вася]привет[/note]").Should().Be("[note=Вася]привет[/note]");
    }

    /// <summary>The quoted attribute is read, and encoded where the tag writes it.</summary>
    [Fact]
    public void EncodeTheQuotedAttribute()
    {
        var html = RenderWithAttributeTag("[note=\"<img src=x onerror=alert(1)>\"]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("note-head\">&lt;img src=x onerror=alert(1)&gt;</div>");
    }

    /// <summary>
    /// A value shown as text is encoded like any other text.
    /// </summary>
    [Fact]
    public void EncodeTheUnquotedAttribute()
    {
        var html = RenderWithAttributeTag("[note=<img src=x onerror=alert(1)>]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// A value ends at an opening bracket, and a tag whose value reached one is
    /// a broken tag whose opening is only its name.
    /// </summary>
    /// <remarks>
    /// This used to be the lever for stored XSS. An encoding pass and the parser
    /// both ended a value at a bracket, which read like agreement, and it was
    /// agreement on two strings: extractions between them replaced a whole tag
    /// with a placeholder carrying no bracket, and the parser read the value the
    /// encoder had refused and put it into the markup as written. There is one
    /// reading of the text now, so the question has one answer: the value is not
    /// read, what follows the name is text, and the tag inside it is a tag.
    /// </remarks>
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
    public void RefuseTheAttribute_WhenACodeBlockStandsInIt()
    {
        var html = RenderWithAttributeTag("[note=<img src=x onerror=alert(1)>[code]x[/code]]hi[/note]");

        html.Should().NotContain("<img src=x");
        html.Should().NotContain("note-head");
    }

    /// <summary>
    /// An image or link spelling the grammar does not accept is text, not an
    /// element.
    /// </summary>
    /// <remarks>
    /// Everything that makes an address safe to put on a page — the scheme white
    /// list, the refusal of loopback and private ranges, the spoiler gate,
    /// referrerpolicy — runs where an image or a link renders, and an attribute
    /// form or an unclosed tag never renders as either: the spellings people
    /// mistype show as they were typed.
    /// </remarks>
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

    /// <summary>
    /// A tag that has no attribute does not acquire one, so text already stored
    /// renders the way it always did.
    /// </summary>
    [Theory]
    [InlineData("[b=1]жирный[/b]")]
    [InlineData("[spoiler=Заголовок]текст[/spoiler]")]
    public void LeaveTheUnquotedAttributeAlone_OnATagThatHasNone(string input)
    {
        RenderComment(input).Should().Be(input);
    }

    // ════════════════════════════════════════════════════════════════
    // Input normalisation: what a character cannot be
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Control characters do not survive the parse, so no consumer downstream
    /// has to decide what to do about them.
    /// </summary>
    /// <remarks>
    /// They used to come out byte for byte, and the search projection was
    /// already stripping them on its own because the result preview marks a
    /// match with two of them - one arriving inside a body would have been read
    /// as a mark. That is one consumer of however many there will be.
    /// </remarks>
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

    /// <summary>
    /// The three control characters a body is written with are not touched.
    /// </summary>
    [Fact]
    public void KeepTheControlCharactersThatMeanSomethingInText()
    {
        var html = RenderComment("a\tb\nc");

        html.Should().Be("a\tb<br />c");
    }

    /// <summary>
    /// A surrogate with no partner is not a character; it becomes the one that
    /// says so, once, here, instead of once per layer below.
    /// </summary>
    [Theory]
    [InlineData(0xD800)]
    [InlineData(0xDC00)]
    public void ReplaceALoneSurrogate(int code)
    {
        var html = RenderComment("a" + Character(code) + "b");

        html.Should().Be("a" + Character(0xFFFD) + "b");
    }

    /// <summary>
    /// A surrogate pair is one character and passes through as it was written.
    /// </summary>
    [Fact]
    public void KeepAPairedSurrogate()
    {
        var html = RenderComment("a\U0001F600b");

        html.Should().Be("a\U0001F600b");
    }

    /// <summary>
    /// The characters that reverse the order text is drawn in do not survive the
    /// parse either.
    /// </summary>
    /// <remarks>
    /// With one of them a word on the page reads differently from the word that
    /// is stored. The marks that only nudge the direction of a neighbouring
    /// character reorder nothing and stay.
    /// </remarks>
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

    /// <summary>
    /// A tag still renders when the same text carries a control character.
    /// </summary>
    /// <remarks>
    /// Kept from the time [img], [link] and [mention] were cut out of the text and
    /// stood in for by a sentinel character until the output was put back
    /// together: a sentinel taken from the range the parser strips would have
    /// been eaten in between, and every picture on the site would have rendered
    /// as its own placeholder. Nothing stands in for a tag any more; the case is
    /// cheap and says so.
    /// </remarks>
    [Fact]
    public void RenderATag_InTextCarryingAControlCharacter()
    {
        var html = RenderComment("a" + Character(0x00) + "[img]https://example.com/a.png[/img]");

        html.Should().Contain("<img");
        html.Should().Contain("https://example.com/a.png");
    }

    // ════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// The character with this code point, written by number because the ones
    /// under test cannot be written any other way and read as nothing when they
    /// can.
    /// </summary>
    private static string Character(int code) => ((char)code).ToString();


    private string RenderComment(string input)
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        return wrapper.RenderHtml(input, DisplayContext(BbSurface.Comment));
    }

    private static string RenderWithAttributeTag(string input) =>
        BbAttributeTagParser.Wrapper.RenderHtml(input, DisplayContext(BbSurface.Comment));

    private string RenderForOutsider(string input)
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var ctx = DisplayContext(BbSurface.GamePost) with
        {
            Viewer = Viewer(ReaderId),
            PostAuthorUserId = AuthorId
        };
        return wrapper.RenderHtml(input, ctx);
    }

    private static RenderContext DisplayContext(BbSurface surface) => new()
    {
        Audience = RenderAudience.Display,
        Surface = surface,
        Viewer = Viewer(ReaderId)
    };

    private static IAuthorizationSubject Viewer(Guid userId) => new TestSubject
    {
        UserId = userId,
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };

}
