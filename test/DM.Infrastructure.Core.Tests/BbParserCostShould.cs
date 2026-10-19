using System;
using System.Diagnostics;
using System.Linq;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Content;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using Xunit;

namespace DM.Infrastructure.Core.Tests;

/// <summary>
/// Tests that time the renderer, run one at a time.
/// </summary>
/// <remarks>A measurement taken beside the rest of the assembly measures the machine.</remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public class BbParserCostCollection
{
    /// <summary>Name the classes join this collection by.</summary>
    public const string Name = "BBCode cost";
}

/// <summary>
/// What the renderer costs as the post grows.
/// </summary>
/// <remarks>The shape of the growth is asserted, not absolute milliseconds.</remarks>
[Collection(BbParserCostCollection.Name)]
public class BbParserCostShould
{
    private readonly IBbParserProvider _parserProvider = new BbParserProvider();

    /// <summary>
    /// An attribute that opens and never closes costs time proportional to the
    /// post, not to its square.
    /// </summary>
    /// <remarks>
    /// Each opening is closed right after it: openings alone nest past the depth guard, which would then be
    /// what is measured.
    /// </remarks>
    [Fact]
    public void ParseUnterminatedAttributeWithoutQuadraticCost()
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
    /// Each shape is row L of bbcodeGrammar.cases.ts, grown; the reader scans a tag once, so no character is
    /// read more than a few times.
    /// </remarks>
    [Theory]
    [InlineData("name-then-spaces")]
    [InlineData("repeated-pairs")]
    [InlineData("repeated-openings")]
    [InlineData("many-quotes")]
    public void ReadTheParametersOfATagWithoutQuadraticCost(string shape)
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
    public void CheckHiddenBlocksOnSaveWithoutQuadraticCost(string fragment)
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
    /// <remarks>Built outside the measured work: the largest lands on the large object heap.</remarks>
    private static System.Collections.Generic.Dictionary<int, string> Inputs(
        Func<int, string> build, int baseLength) =>
        new[] { baseLength, baseLength * 4, baseLength * 16 }.ToDictionary(length => length, build);

    /// <summary>
    /// The same for the whole render.
    /// </summary>
    /// <remarks>
    /// Every tag once cut out before the parser, and one with an encoded attribute, each unclosed inside a
    /// closed note; every [code] asks once for its closing tag.
    /// </remarks>
    [Fact]
    public void RenderTagSoupWithoutQuadraticCost()
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
    /// The soups of the generated checks repeated to tens of kilobytes, from a fixed sequence of seeds; a soup
    /// that leaves tags open is passed over, it would measure the depth guard.
    /// </remarks>
    [Fact]
    public void ReadGeneratedTextsWithoutQuadraticCost()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.GamePost);
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
    /// Halfway between linear growth of a quadrupled input (4) and quadratic growth (16).
    /// </summary>
    private const double QuadraticThreshold = 8.0;

    /// <summary>
    /// The gentlest of up to three readings of the steepest step, the first one
    /// under the threshold ending the search.
    /// </summary>
    /// <remarks>Outside load can push linear code over the threshold for a reading, never quadratic code under it.</remarks>
    private static double MeasureGrowth(Action<int> work, int baseLength)
    {
        const int readings = 3;

        var gentlest = double.MaxValue;
        for (var reading = 0; reading < readings && gentlest >= QuadraticThreshold; reading++)
        {
            gentlest = Math.Min(gentlest, SteepestStep(work, baseLength));
        }

        return gentlest;
    }

    /// <summary>
    /// The steepest step in cost across three inputs, each four times the last.
    /// </summary>
    /// <remarks>
    /// Three points, because a fixed overhead flattens the first step. Sizes are timed over repetitions in
    /// interleaved rounds, the best kept; collector pauses are taken out, and the warm-up is not counted.
    /// </remarks>
    private static double SteepestStep(Action<int> work, int baseLength)
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
                var paused = GC.GetTotalPauseDuration();
                var stopwatch = Stopwatch.StartNew();
                for (var repetition = 0; repetition < repetitions; repetition++)
                {
                    work(lengths[size]);
                }

                stopwatch.Stop();
                var spent = stopwatch.Elapsed - (GC.GetTotalPauseDuration() - paused);
                best[size] = Math.Min(best[size], spent.Ticks);
            }
        }

        var steepest = 0.0;
        for (var size = 1; size < best.Length; size++)
        {
            steepest = Math.Max(steepest, (double)best[size] / Math.Max(best[size - 1], 1));
        }

        return steepest;
    }


    private static IAuthorizationSubject Viewer() => BbVisibilityRender.Viewer(UserRole.RegularUser, Guid.Parse("44444444-4444-4444-4444-444444444444"));

}
