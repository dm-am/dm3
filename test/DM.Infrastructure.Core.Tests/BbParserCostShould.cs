using System;
using System.Diagnostics;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Tests that time the renderer, run one at a time.
/// </summary>
/// <remarks>
/// A measurement taken while the rest of the assembly is running on the same
/// cores is a measurement of the machine, and the failures it produces are the
/// kind that get rerun until they pass rather than read.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class BbParserCostCollection
{
    /// <summary>Name the classes join this collection by.</summary>
    public const string Name = "BBCode cost";
}

/// <summary>
/// What the renderer costs as the post grows.
/// </summary>
/// <remarks>
/// Absolute milliseconds are not asserted - they say more about the machine than
/// about the code, and about which machine ran it. The shape of the growth is
/// what was wrong, and the shape is what is asserted.
/// </remarks>
[Collection(BbParserCostCollection.Name)]
public class BbParserCostShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    /// <summary>
    /// An attribute that opens and never closes costs time proportional to the
    /// post, not to its square.
    /// </summary>
    /// <remarks>
    /// The lazy scan in the reader's pattern restarted from every opening and
    /// walked the whole remainder, so four times the text cost sixteen times the
    /// work: 4 KB / 158 ms, 16 KB / 2 039 ms, 64 KB / 28 907 ms, measured before
    /// the fix, on a render path that is not cached and that every reader of the
    /// page pays.
    ///
    /// Each opening is closed right after it. A broken tag is a node like any
    /// other, so openings alone nest, and past the depth guard the parse stops
    /// at the guard rather than at the end of the text - a measurement of the
    /// guard, not of the scan.
    /// </remarks>
    [Fact]
    public void ParseUnterminatedAttribute_WithoutQuadraticCost()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        MeasureGrowth(
                length => parser.Parse(BbTestText.Repeat("[quote=\"x [/quote]", length) + "tail"),
                baseLength: 250)
            .Should().BeLessThan(QuadraticThreshold);
    }

    /// <summary>
    /// The shapes of the parameter grammar that cost the square of the input in
    /// a reader that backtracks: a name followed by thousands of spaces, many
    /// pairs, many openings each running to the next, many quotes in one value.
    /// </summary>
    /// <remarks>
    /// Each shape is the one row L of bbcodeGrammar.cases.ts holds, grown. The
    /// reader scans a tag once, stopping at the next opening bracket at the
    /// latest, and a value asks at each quote only whether "]" or the next
    /// name= follows it - so no character is read more than a few times, however
    /// the text is arranged. The openings are closed one by one for the reason
    /// the test above gives.
    /// </remarks>
    [Theory]
    [InlineData("name-then-spaces")]
    [InlineData("repeated-pairs")]
    [InlineData("repeated-openings")]
    [InlineData("many-quotes")]
    public void ReadTheParametersOfATag_WithoutQuadraticCost(string shape)
    {
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);
        Func<int, string> text = shape switch
        {
            "name-then-spaces" => length => "[img" + BbTestText.Repeat(" ", length * 8) + "x",
            "repeated-pairs" => length => "[img" + BbTestText.Repeat(" a=\"1\"", length) + " x",
            "repeated-openings" => length => BbTestText.Repeat("[img width=\"x [/img]", length),
            "many-quotes" => length => "[spoiler text=\"" + BbTestText.Repeat("\"", length * 8) + " x",
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };

        var inputs = Inputs(text, baseLength: 250);

        MeasureGrowth(length => parser.Parse(inputs[length]).ToHtml(), baseLength: 250)
            .Should().BeLessThan(QuadraticThreshold);
    }

    /// <summary>
    /// The save path reads hidden blocks by a scanner of its own, over the same
    /// shapes and the ones only a hidden block has: an attribute that never ends
    /// and a [noparse] nobody closed.
    /// </summary>
    [Theory]
    [InlineData("[private=\"")]
    [InlineData("[private ")]
    [InlineData("[noparse][private]")]
    [InlineData("[private=\"x\"][/private][noparse]")]
    public void CheckHiddenBlocksOnSave_WithoutQuadraticCost(string fragment)
    {
        var inputs = Inputs(length => BbTestText.Repeat(fragment, length), baseLength: 250);

        MeasureGrowth(
                length =>
                {
                    var text = inputs[length];
                    PrivateBlockMarkup.IsBalanced(text);
                    PrivateBlockMarkup.ContainsPrivateMarkup(text);
                    PrivateBlockMarkup.ReadAddresseeAttributes(text);
                },
                baseLength: 250)
            .Should().BeLessThan(QuadraticThreshold);
    }

    /// <summary>
    /// The inputs of the three sizes, built before the clock starts.
    /// </summary>
    /// <remarks>
    /// Building one inside the measured work measures the allocator: the largest
    /// of them lands on the large object heap, which costs out of proportion to
    /// its length and reads as a quadratic step on code that is linear.
    /// </remarks>
    private static System.Collections.Generic.Dictionary<int, string> Inputs(
        Func<int, string> build, int baseLength) =>
        new[] { baseLength, baseLength * 4, baseLength * 16 }.ToDictionary(length => length, build);

    /// <summary>
    /// The same for the whole render.
    /// </summary>
    /// <remarks>
    /// The input is the one from the analysis that found the extraction passes
    /// this renderer used to run before the parser: every tag they cut out and a
    /// tag whose attribute one of them encoded, each left unclosed. Six of the
    /// ten passes carried the same lazy scan the reader did, so the cost was paid
    /// twice over - 17 KB of it cost 3.6 s of CPU per render and 34 KB cost
    /// 16.5 s. The passes are gone; the input stays as the record of what they
    /// cost.
    ///
    /// Each piece stands inside a note that is closed, for the reason the first
    /// test gives: a tag nobody closed is a node, and openings alone would nest
    /// past the depth guard. The [code] in it is still never closed, and every
    /// one of them asks where its closing tag is - the question the reader
    /// answers once for the whole text rather than once per opening.
    /// </remarks>
    [Fact]
    public void RenderTagSoup_WithoutQuadraticCost()
    {
        var wrapper = BbAttributeTagParser.Wrapper;
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.Comment,
            Viewer = Viewer()
        };
        var inputs = Inputs(
            length => BbTestText.Repeat("[note][img][code][link=x][note=\"[img=1][/note][/note]", length),
            baseLength: 100);

        MeasureGrowth(length => wrapper.RenderHtml(inputs[length], ctx), baseLength: 100)
            .Should().BeLessThan(QuadraticThreshold);
    }

    /// <summary>
    /// A generated text written out again and again costs the renderer, the
    /// plain text and the save path time in proportion to its length, whatever
    /// it holds.
    /// </summary>
    /// <remarks>
    /// The shapes above are the ones somebody found. These are the soups of the
    /// generated checks (markupAlphabet.ts, MarkupGenerator), each repeated
    /// until the text is some tens of kilobytes: a piece that makes any reader
    /// look back or look ahead once per opening shows as a quadratic step here
    /// without anybody having thought of it. A soup that leaves tags open is
    /// passed over, because repeated it nests past the depth guard, and the
    /// guard, not the reading, is then what gets measured; the ones taken are
    /// the first few of a fixed sequence of seeds, the same on every run.
    /// </remarks>
    [Fact]
    public void ReadGeneratedTexts_WithoutQuadraticCost()
    {
        var parser = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.GamePost);
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.GamePost,
            Viewer = Viewer(),
            PostAuthorUserId = Viewer().UserId
        };
        var plain = RenderContext.ForPlainText() with { Surface = BbSurface.GamePost };
        // A reader the blocks are erased for: the tags they leave empty are
        // looked for, and left out, on the same walk (BBCodeParser.Nodes.Visibility).
        var stranger = ctx with { PostAuthorUserId = Guid.NewGuid() };
        var tagNames = new BbTagNameCase(_parserProvider);

        // Each reader on its own: timed together, the renderer, which costs
        // the most, hid a square step in the save path under its own line.
        var readers = new (string Name, Action<string> Read)[]
        {
            ("the page", text => parser.RenderHtml(text, ctx)),
            ("the page of a stranger", text => parser.RenderHtml(text, stranger)),
            ("the plain text", text => parser.RenderText(text, plain)),
            ("the save path", text =>
            {
                PrivateBlockMarkup.IsBalanced(text);
                PrivateBlockMarkup.ContainsPrivateMarkup(text);
                PrivateBlockMarkup.ReadAddresseeAttributes(text);
                tagNames.ToLowerCase(text, BbTagSet.Common);
            })
        };

        var measured = 0;
        for (var seed = 0UL; measured < 2 && seed < 400; seed++)
        {
            var chunk = DM.Testing.Markup.MarkupGenerator.Soup(new DM.Testing.Markup.MarkupRandom(seed)).Text;

            // Long enough to be timed, and holding hiding markup, without which
            // the save path reads nothing at all.
            if (chunk.Length < 40 || !chunk.Contains("[private", StringComparison.OrdinalIgnoreCase)) continue;

            var times = Math.Max(1, 1500 / chunk.Length);
            var inputs = Inputs(length => BbTestText.Repeat(chunk, length), baseLength: times);
            try
            {
                parser.Parse(inputs[times * 16]);
            }
            catch (BBCodeParser.BbParserException)
            {
                continue;
            }

            measured++;
            foreach (var (name, read) in readers)
            {
                MeasureGrowth(length => read(inputs[length]), baseLength: times)
                    .Should().BeLessThan(QuadraticThreshold,
                        $"{name} of {DM.Testing.Markup.MarkupLiteral.Of(chunk)} written out again and again");
            }
        }

        measured.Should().Be(2, "a check of no text passes");
    }

    /// <summary>
    /// Halfway between the linear growth expected of a quadrupled input (4) and
    /// the quadratic growth measured before the fix (16).
    /// </summary>
    private const double QuadraticThreshold = 8.0;

    /// <summary>
    /// The steepest step in cost across three inputs, each four times the last.
    /// </summary>
    /// <remarks>
    /// Three points rather than two, because two do not separate the shapes
    /// reliably. Every measurement carries a fixed cost that does not grow with
    /// the input, and that cost flattens the first step: with an overhead three
    /// times the linear part, quadrupling the text costs 4.75 times as much on
    /// quadratic code - under any threshold linear code has to pass. The same
    /// overhead over the second step gives 13.6, because by then it is a rounding
    /// error next to the work. Linear code stays near 4 at both steps.
    ///
    /// Each size is timed over a run of repetitions rather than once, because a
    /// single linear pass over a few kilobytes is shorter than the clock is
    /// steady, and the rounds are interleaved rather than run size by size: a
    /// machine that goes busy halfway through then slows all three sizes instead
    /// of whichever one it happened to reach. The best round of each size is the
    /// one kept, for the same reason - contention only ever adds time.
    ///
    /// The warm-up is not counted: the first call of each size pays for jitting
    /// the regular expressions, a fixed cost that would otherwise be charged to
    /// the smallest input, where it is largest in proportion.
    /// </remarks>
    private static double MeasureGrowth(Action<int> work, int baseLength)
    {
        const int repetitions = 40;
        const int rounds = 5;

        var lengths = new[] { baseLength, baseLength * 4, baseLength * 16 };
        var best = new long[lengths.Length];
        Array.Fill(best, long.MaxValue);

        foreach (var length in lengths)
        {
            work(length);
        }

        for (var round = 0; round < rounds; round++)
        {
            for (var size = 0; size < lengths.Length; size++)
            {
                var stopwatch = Stopwatch.StartNew();
                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    work(lengths[size]);
                }

                stopwatch.Stop();
                best[size] = Math.Min(best[size], stopwatch.ElapsedTicks);
            }
        }

        var steepest = 0.0;
        for (var size = 1; size < best.Length; size++)
        {
            steepest = Math.Max(steepest, (double)best[size] / Math.Max(best[size - 1], 1));
        }

        return steepest;
    }


    private static IAuthorizationSubject Viewer() => new TestSubject
    {
        UserId = Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Role = UserRole.RegularUser,
        IsAuthenticated = true,
        AccessPolicy = AccessPolicy.NotSpecified
    };

}
