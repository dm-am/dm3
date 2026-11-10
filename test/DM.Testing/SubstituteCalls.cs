using System.Linq;
using AwesomeAssertions;
using NSubstitute;

namespace DM.Testing;

/// <remarks>NSubstitute has no VerifyNoOtherCalls, so the accounted calls are named and checked against ReceivedCalls().</remarks>
public static class SubstituteCalls
{
    /// <summary>Asserts the substitute was never called at all.</summary>
    public static void ShouldHaveReceivedNoCalls<T>(this T substitute) where T : class =>
        substitute.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Should()
            .BeEmpty("nothing was expected of this collaborator");

    /// <remarks>The composition is checked, not the count: a count stays green when traffic moves to another member.</remarks>
    public static void ShouldHaveReceivedNothingElse<T>(this T substitute, params string[] expectedCalls)
        where T : class =>
        substitute.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Should()
            .BeEquivalentTo(expectedCalls, "no call beyond the ones asserted above was expected");
}
