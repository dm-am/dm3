using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Refused sign-in attempts by reason, which tells credential stuffing from typos.
/// </summary>
/// <remarks>
/// The label is the enum member, nothing about who tried. Rate-limiter refusals
/// come before any credential is read and are read off the request series.
/// </remarks>
public static class AuthenticationMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Authentication";

    private static readonly Meter Meter = new(MeterName, DmActivitySource.Version);

    /// <summary>
    /// Login attempts that were refused. Attributes: <c>reason</c> (the
    /// authentication error the attempt ended on).
    /// </summary>
    public static readonly Counter<long> LoginFailed = Meter.CreateCounter<long>(
        "dm.auth.login_failed", null, "Login attempts refused, by the reason they were refused");

    /// <summary>Attribute set naming why an attempt was refused.</summary>
    /// <param name="reason">Authentication error, as the enum declares it.</param>
    public static KeyValuePair<string, object?> Reason(string reason) => new("reason", reason);
}
