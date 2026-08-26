using System;
using System.Diagnostics;
using AwesomeAssertions;
using DM.Domain.Core.Authorization;
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
    /// </remarks>
    [Fact]
    public void ParseUnterminatedAttribute_WithoutQuadraticCost()
    {
        var parser = _parserProvider.GetForSurface(BbSurface.Comment);

        MeasureGrowth(
                length => parser.Parse(BbTestText.Repeat("[quote=\"", length) + "tail"),
                baseLength: 250)
            .Should().BeLessThan(QuadraticThreshold);
    }

    /// <summary>
    /// The same for the whole render, which is where the extraction passes in
    /// this wrapper are spent.
    /// </summary>
    /// <remarks>
    /// The input is the one from the analysis: every tag the wrapper cuts out
    /// before the parser, each left unclosed. Six of its ten passes carried the
    /// same lazy scan the reader did, so the cost was paid twice over - 17 KB of
    /// it cost 3.6 s of CPU per render and 34 KB cost 16.5 s.
    /// </remarks>
    [Fact]
    public void RenderTagSoup_WithoutQuadraticCost()
    {
        var wrapper = (BbParserWrapper)_parserProvider.GetForSurface(BbSurface.Comment);
        var ctx = new RenderContext
        {
            Audience = RenderAudience.Display,
            Surface = BbSurface.Comment,
            Viewer = Viewer()
        };

        MeasureGrowth(
                length => wrapper.RenderHtml(BbTestText.Repeat("[img][code][link=x][quote=\"[img=1]", length), ctx),
                baseLength: 100)
            .Should().BeLessThan(QuadraticThreshold);
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
