using System;
using System.Linq;
using DM.Domain.Core.Exceptions;
using DM.Web.API.Shared.Http;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Features.Account;

/// <summary>
/// Recording of the responses that tell a caller whether an identifier is registered
/// </summary>
/// <remarks>
/// These surfaces disclose existence by design (the recorded exception in
/// docs/conventions/SECURITY.md); a bulk scan of them must still leave a trace,
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
        // Not the probed identifier: entries live a month, a list of what scans looked for.
        logger.LogInformation(
            "Identifier existence disclosed by {Surface} as {Outcome} to {ClientAddress}",
            surface, outcome, httpContext.GetClientAddress());

    /// <summary>
    /// Whether a refusal told the caller that the field's value is somebody else's,
    /// from the validator or from the write that lost the race for the value
    /// </summary>
    public static bool DisclosesTaken(this Exception refusal, string field) => refusal switch
    {
        ValidationException validation => validation.Errors.Any(e =>
            e.PropertyName == field && e.ErrorMessage == ValidationError.Taken),
        HttpBadRequestException badRequest => badRequest.ValidationErrors.TryGetValue(field, out var error) &&
                                              error == ValidationError.Taken,
        _ => false
    };
}
