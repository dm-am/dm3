using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace DM.Infrastructure.Core.Tracing;

/// <summary>
/// Metrics of the front door. What is counted here is every attempt to get in
/// that was refused, and the reason it was refused.
/// </summary>
/// <remarks>
/// The request series says a route answered 4xx, which is the same answer for a
/// mistyped password, an unfinished registration and a banned account — so a
/// credential-stuffing run reads exactly like a busy evening of typos, and the
/// lockouts it causes read as a rise in the same undifferentiated number. The
/// reason is the whole information: thousands of "wrong login" is somebody
/// walking a list of addresses, thousands of "wrong password" against few
/// addresses is somebody walking a list of passwords, and a rise in "account
/// locked" is the first of those two having worked long enough to matter.
///
/// Refusals from the rate limiter are deliberately not here. They happen before
/// anything reads a credential, so the code that could count them has not run;
/// they are read off the request series by status instead.
///
/// The reason is the enum member, which bounds the label to what the type
/// declares. No identifier, address or agent is attached: this counts attempts,
/// and who made them is a question for the login records.
/// </remarks>
public static class AuthenticationMetrics
{
    /// <summary>Meter name for OTel registration.</summary>
    public const string MeterName = "DM.Authentication";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

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
