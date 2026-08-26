using System;
using System.Linq.Expressions;
using DM.Infrastructure.Persistence.Shared.Queries;
using AwesomeAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.Shared.Queries;

/// <summary>
/// The splicer inlines a stored rule into a projection lambda: the marker
/// call disappears, the rule's body appears with the argument substituted
/// for its parameter, and a rule that itself splices another rule resolves
/// all the way down. Every list projection of the site rides on this
/// rewrite, so its mechanics get their own facts.
/// </summary>
public class ExpressionSplicerShould
{
    private static readonly Expression<Func<int, int>> AddOne = x => x + 1;

    private static readonly Expression<Func<int, int>> DoubleThenAddOne =
        x => AddOne.Splice(x * 2);

    [Fact]
    public void SubstituteTheArgumentForTheRuleParameter()
    {
        var expanded = ExpressionSplicer.Expand<Func<int, int>>(y => AddOne.Splice(y * 10));

        expanded.ToString().Should().NotContain("Splice", "the marker must be rewritten away");
        expanded.Compile()(3).Should().Be(31, "the rule body runs over the substituted argument");
    }

    [Fact]
    public void ResolveASpliceNestedInsideASplicedRule()
    {
        var expanded = ExpressionSplicer.Expand<Func<int, int>>(y => DoubleThenAddOne.Splice(y + 1));

        expanded.ToString().Should().NotContain("Splice");
        expanded.Compile()(4).Should().Be(11, "(4 + 1) * 2 + 1: both layers of rule inlined");
    }

    [Fact]
    public void RefuseToExecuteAMarkerThatWasNeverExpanded()
    {
        var act = () => AddOne.Splice(1);

        act.Should().Throw<InvalidOperationException>(
            "the marker exists for the rewriter, not for the runtime");
    }
}
