using System.Linq;
using AwesomeAssertions;
using NSubstitute;

namespace DM.Testing;

/// <summary>
/// Assertions about everything a substitute was <em>not</em> asked to do.
/// </summary>
/// <remarks>
/// NSubstitute has no equivalent of Moq's <c>VerifyNoOtherCalls</c>, because it has
/// no notion of a call being "verified": <c>Received</c> asks a question and moves
/// on. So the calls a test accounts for are named here rather than inferred, and
/// the assertion is over <c>ReceivedCalls()</c> - the whole record of the
/// substitute, including the calls no <c>Received</c> line mentions. Asserting the
/// absence of collaborator traffic whole is worth this: a wall of
/// <c>DidNotReceive()</c> lines covers only the members somebody thought to list.
/// </remarks>
public static class SubstituteCalls
{
    /// <summary>Asserts the substitute was never called at all.</summary>
    public static void ShouldHaveReceivedNoCalls<T>(this T substitute) where T : class =>
        substitute.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Should()
            .BeEmpty("nothing was expected of this collaborator");

    /// <summary>
    /// Asserts the substitute received exactly the named calls and nothing else.
    /// </summary>
    /// <param name="substitute">The substitute under inspection.</param>
    /// <param name="expectedCalls">Names of the members the test accounts for, in any order.</param>
    /// <remarks>
    /// The composition is checked rather than the count, because a count is not tied
    /// to the <c>Received</c> lines it is supposed to summarise: it stays green when
    /// the traffic moves to a different member, and it has to be recounted by hand
    /// every time those lines change. Naming the members makes the assertion state
    /// which traffic is expected, so it fails on a substituted call the same way it
    /// fails on an extra one.
    /// </remarks>
    public static void ShouldHaveReceivedNothingElse<T>(this T substitute, params string[] expectedCalls)
        where T : class =>
        substitute.ReceivedCalls().Select(c => c.GetMethodInfo().Name).Should()
            .BeEquivalentTo(expectedCalls, "no call beyond the ones asserted above was expected");
}
