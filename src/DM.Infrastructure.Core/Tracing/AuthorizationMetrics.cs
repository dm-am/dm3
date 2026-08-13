using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of the authorization layer. What is counted here is a defect, not
/// traffic: every measurement is a question the application could not answer.
/// </summary>
/// <remarks>
/// Asking about an intention nobody wrote a resolver for is refused, which is the
/// right answer to a question with no answer — and it is indistinguishable from a
/// refusal the rules intended. The reader is told they may not do a thing they may
/// in fact do, and the only trace is one line in a store nobody reads until
/// somebody complains.
///
/// So the counter is what makes the safe default honest. Refusing without saying
/// so is how a whole feature quietly stops working for everybody.
/// </remarks>
public static class AuthorizationMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Authorization";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    /// <summary>
    /// Authorization questions that reached no resolver. Attributes:
    /// <c>intention</c> (intention type), <c>target</c> (target type, or none).
    /// </summary>
    public static readonly Counter<long> ResolverMissing = Meter.CreateCounter<long>(
        "dm.authorization.resolver_missing", null,
        "Authorization questions no resolver answered, refused by default");
}
