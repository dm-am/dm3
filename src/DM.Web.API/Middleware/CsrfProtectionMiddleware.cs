using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for CSRF protection via Origin/Referer validation
/// </summary>
/// <remarks>
/// The API authenticates with a session cookie (BFF pattern), so the browser attaches
/// credentials to a cross-site request by itself. Two controls stand here, and
/// neither is the whole of it: SameSite=Lax keeps the session off a request another
/// site started, and this check refuses the ones that arrive naming an origin the
/// site does not answer on. The requests that carry no origin at all are covered by
/// the first alone, which is why that branch passes them.
/// </remarks>
public class CsrfProtectionMiddleware
{
    private static readonly HashSet<string> StateChangingMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

    /// <summary>
    /// Creates new CSRF protection middleware
    /// </summary>
    public CsrfProtectionMiddleware(RequestDelegate next, ILogger<CsrfProtectionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Validates Origin/Referer for state-changing requests
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IOptions<SiteAddressConfiguration> settings)
    {
        // Only check state-changing methods
        if (!StateChangingMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Skip validation for health checks and metrics
        var path = context.Request.Path.Value;
        if (path != null && (path.StartsWith("/_") || path.StartsWith("/metrics")))
        {
            await _next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault()
            ?? ExtractOriginFromReferer(context.Request.Headers.Referer.FirstOrDefault());

        // No Origin and no Referer: a non-browser caller (Postman, curl). Allowed
        // through because browsers attach Origin to every state-changing request, so a
        // cross-site form post never reaches this branch. What guards the branch is
        // SameSite=Lax on the session cookie: a cross-site post arrives without the
        // cookie and is therefore unauthenticated.
        if (string.IsNullOrEmpty(origin))
        {
            await _next(context);
            return;
        }

        var allowed = settings.Value.BrowserOrigins();
        if (!IsOriginAllowed(origin, allowed))
        {
            _logger.LogWarning(
                "CSRF protection blocked request from origin {Origin}. Allowed: {AllowedOrigins}",
                origin, string.Join(", ", allowed));

            // ErrorHandlingMiddleware assembles the refusal body, and nothing else
            // does: it sits above this one in the pipeline, so an exception thrown
            // here reaches it. Building ProblemDetails here gave an answer with no
            // traceId and no type, a shape no other refusal has. The correlation
            // token exists precisely to tie a refusal to its log entry, and this was
            // the one place it was missing.
            throw new HttpException(HttpStatusCode.Forbidden, "Запрос пришел с недопустимого адреса");
        }

        await _next(context);
    }

    private static string? ExtractOriginFromReferer(string? referer) =>
        SiteAddressConfiguration.OriginOf(referer);

    /// <summary>
    /// Whether an origin is one this site answers on.
    /// </summary>
    /// <remarks>
    /// Both sides go through the same normaliser, so a trailing slash, a spelt-out
    /// default port or a capital letter in the host cannot make one list mean two
    /// policies - UseCors is handed the same list and compares an origin whole.
    /// </remarks>
    private static bool IsOriginAllowed(string origin, IReadOnlyList<string> allowedOrigins) =>
        SiteAddressConfiguration.OriginOf(origin) is { } normalized &&
        allowedOrigins.Contains(normalized, StringComparer.Ordinal);
}
