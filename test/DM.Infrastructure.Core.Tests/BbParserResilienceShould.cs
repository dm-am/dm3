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
    [Fact]
    public void RenderBothOutputs_AtTheDeepestAcceptedNesting()
    {
        const int depth = 512;
        var input = BbTestText.Repeat("[b]", depth) + "x";
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = DisplayContext(BbSurface.Comment);

        wrapper.RenderHtml(input, ctx).Should().EndWith("x" + BbTestText.Repeat("</strong>", depth));
        wrapper.RenderText(input, ctx).Should().Be("x");
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
    /// The save path accepts the line-break spelling, which is why the render
    /// path has to hold on its own.
    /// </summary>
    /// <remarks>
    /// The balance check reads the block with a pattern that does cross a line
    /// break, so from where it stands the post is well formed. Nothing between
    /// it and the page disagreed until the parser, and the parser disagreed
    /// silently.
    /// </remarks>
    [Fact]
    public void AcceptTheLineBreakSpelling_AtTheSavePath()
    {
        PrivateBlockMarkup.IsBalanced("[private=\"Гэн\nдальф\"]тайна[/private]")
            .Should().BeTrue();
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
    // Unquoted attribute
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// [quote=Author] is the tag it looks like.
    /// </summary>
    /// <remarks>
    /// It used not to be, and the failure was worse than a tag not rendering:
    /// the opening tag stayed on the page as text while the closing [/quote] was
    /// still consumed, so the reader saw markup at the top of a quotation with
    /// nothing marking its end. The editor rendered it as a quote in preview,
    /// which is how it got written in the first place.
    /// </remarks>
    [Fact]
    public void ReadTheUnquotedAttribute()
    {
        var html = RenderComment("[quote=Вася]привет[/quote]");

        html.Should().Contain("quote-author\">Вася</div>");
        html.Should().Contain("привет");
        html.Should().NotContain("[quote");
        html.Should().NotContain("[/quote]");
    }

    /// <summary>
    /// An unquoted value is encoded exactly like a quoted one before it reaches
    /// the tag template.
    /// </summary>
    /// <remarks>
    /// [quote] is declared secure: false, which means the parser substitutes the
    /// value into its markup as written. Teaching the parser to read the
    /// unquoted spelling without teaching the wrapper's encoder the same
    /// spelling would have opened a stored cross-site scripting hole on every
    /// surface that renders a quotation.
    /// </remarks>
    [Fact]
    public void EncodeTheUnquotedAttribute()
    {
        var html = RenderComment("[quote=<img src=x onerror=alert(1)>]hi[/quote]");

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// The attribute value is encoded even when the tag it carries is one this
    /// wrapper extracts before the parser runs.
    /// </summary>
    /// <remarks>
    /// The encoder and the parser both end an attribute value at an opening
    /// bracket, which reads like agreement. It was agreement on two different
    /// strings: the encoder used to run first, and the extractions between it
    /// and the parser replace a whole tag with a placeholder that carries no
    /// bracket. So the value below — refused by the encoder for the bracket in
    /// the middle of it — arrived at the parser with that bracket gone, was read,
    /// and went into &lt;div class="quote-author"&gt; as written, because [quote] is
    /// declared secure: false and the client binds the result through v-html.
    /// Stored XSS on every surface that renders a quotation.
    ///
    /// One row per tag the wrapper extracts, because each of them is the same
    /// lever.
    /// </remarks>
    [Theory]
    [InlineData("[quote=<img src=x onerror=alert(1)>[img]https://example.com/a.png[/img]]hi[/quote]")]
    [InlineData("[quote=\"<img src=x onerror=alert(1)>[img]https://example.com/a.png[/img]\"]hi[/quote]")]
    [InlineData("[quote=<img src=x onerror=alert(1)>[link=t]https://example.com[/link]]hi[/quote]")]
    [InlineData("[quote=<img src=x onerror=alert(1)>[mention=\"Вася\"]]hi[/quote]")]
    public void EncodeTheAttribute_EvenWhereExtractionRemovesTheBracket(string input)
    {
        var html = RenderComment(input);

        html.Should().NotContain("<img src=x");
        html.Should().Contain("&lt;img src=x onerror=alert(1)&gt;");
    }

    /// <summary>
    /// The one extraction that is put back after the encoding runs cannot smuggle
    /// a value either.
    /// </summary>
    /// <remarks>
    /// A [code] or [noparse] block is lifted out first and restored last, after
    /// the encoding, because its content is shown as written and must not be
    /// rewritten. So restoration does put brackets back into an attribute value
    /// the encoder had already read - and that direction is the safe one: a
    /// bracket can only turn a value the parser would have read into one it
    /// refuses, never the other way round. Here the tag is refused and its value
    /// shows as the text the author typed, encoded.
    /// </remarks>
    [Fact]
    public void RefuseTheAttribute_WhenAVerbatimBlockIsRestoredIntoIt()
    {
        var html = RenderComment("[quote=<img src=x onerror=alert(1)>[code]x[/code]]hi[/quote]");

        html.Should().NotContain("<img src=x");
        html.Should().NotContain("quote-author");
    }

    /// <summary>
    /// An image or link spelling this wrapper did not extract is text, not an
    /// element.
    /// </summary>
    /// <remarks>
    /// Everything that makes an address safe to put on a page lives in this
    /// wrapper — the scheme white list, the refusal of loopback and private
    /// ranges, the spoiler gate, referrerpolicy — and it only runs on spellings
    /// its patterns catch. All of them require a closing tag, so the ones that
    /// got past were the unclosed ones, which is ordinary mistyping rather than
    /// a trick: the parser had [img] and [link] in its tag set too and rendered
    /// them from templates that substitute the address as written. The tags are
    /// no longer in that set, so what the wrapper did not extract is not a tag
    /// at all.
    /// </remarks>
    [Theory]
    [InlineData("[link=vbscript:msgbox(1)]click")]
    [InlineData("[link=\"vbscript:msgbox(1)\"]click")]
    [InlineData("[link=http://169.254.169.254/latest/meta-data]metadata")]
    [InlineData("[link=http://127.0.0.1:9200/_all]probe")]
    [InlineData("[img=200]")]
    [InlineData("[img]https://example.com/a.png")]
    public void LeaveAnUnextractedImageOrLinkAsText(string input)
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
    /// <remarks>
    /// The reader accepts an unquoted value only for a tag declaring one -
    /// quote, private, img and link. Without that restriction [b=1] in prose,
    /// which is text today, would start emitting a bold run tomorrow, and
    /// nobody would connect the change to a parser fix.
    /// </remarks>
    [Theory]
    [InlineData("[b=1]жирный[/b]")]
    [InlineData("[spoiler=Заголовок]текст[/spoiler]")]
    public void LeaveTheUnquotedAttributeAlone_OnATagThatHasNone(string input)
    {
        var openingTag = input[..(input.IndexOf(']') + 1)];

        var html = RenderComment(input);

        html.Should().Contain(openingTag);
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
    /// The characters that reorder text for display go through untouched.
    /// </summary>
    /// <remarks>
    /// This records where the normalisation deliberately stops, not that
    /// stopping there is right. Removing them changes what a reader sees on a
    /// page, which is a product decision; the rest of the normalisation changes
    /// nothing anybody could see and needed no such decision.
    /// </remarks>
    [Theory]
    [InlineData(0x202E)]
    [InlineData(0x2066)]
    public void LeaveTheTextDirectionOverridesAlone(int code)
    {
        var html = RenderComment("a" + Character(code) + "b");

        html.Should().Be("a" + Character(code) + "b");
    }

    /// <summary>
    /// A tag the wrapper lifts out of the text is still put back when the same
    /// text carries a control character.
    /// </summary>
    /// <remarks>
    /// The wrapper stands an extracted [img] / [link] / [mention] on a sentinel
    /// character and hands the result, sentinel and all, to the parser - so the
    /// moment the parser began stripping control characters, a sentinel taken
    /// from that range would have been eaten between the two halves of the
    /// wrapper and every picture on the site would have rendered as its own
    /// placeholder.
    /// </remarks>
    [Fact]
    public void RestoreAnExtractedTag_InTextCarryingAControlCharacter()
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
