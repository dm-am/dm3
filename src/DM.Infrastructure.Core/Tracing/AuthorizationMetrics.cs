using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Intentions no resolver answered: refused by default, which otherwise reads
/// exactly like a refusal the rules meant.
/// </summary>
public static class AuthorizationMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Authorization";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

    /// <summary>
    /// Authorization questions that reached no resolver. Attributes:
    /// <c>intention</c> (intention type), <c>target</c> (target type, or none).
    /// </summary>
    public static readonly Counter<long> ResolverMissing = Meter.CreateCounter<long>(
        "dm.authorization.resolver_missing", null,
        "Authorization questions no resolver answered, refused by default");
}
