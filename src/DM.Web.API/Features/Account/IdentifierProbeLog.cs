using DM.Web.API.Shared.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Account;

/// <summary>
/// Recording of the responses that tell a caller whether an identifier is registered
/// </summary>
/// <remarks>
/// Registration needs to say "this email is taken" and account recovery needs to
/// say which letter it sent, so both surfaces disclose existence by design — see
/// the recorded exception in docs/conventions/SECURITY.md. What is not acceptable
/// is that a bulk scan of those surfaces leaves no trace, and this is that trace:
/// one event per disclosing answer, countable per address.
/// </remarks>
internal static class IdentifierProbeLog
{
    /// <summary>
    /// Records that a response disclosed the existence of an identifier
    /// </summary>
    /// <param name="logger">Logger of the surface that answered</param>
    /// <param name="httpContext">HTTP context of the probe</param>
    /// <param name="surface">Endpoint that answered</param>
    /// <param name="outcome">What the caller learned</param>
    public static void IdentifierDisclosed(this ILogger logger,
        HttpContext httpContext, string surface, string outcome) =>
        // The probed identifier is deliberately absent: log entries have no
        // retention, and writing it would turn every scan into a stored list of
        // the addresses it went looking for.
        logger.LogInformation(
            "Identifier existence disclosed by {Surface} as {Outcome} to {ClientAddress}",
            surface, outcome, httpContext.GetClientAddress());
}
