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
/// The session cookie rides on a cross-site request by itself. SameSite=Lax keeps
/// it off a request another site started; this check refuses the ones that name
/// an origin the site does not answer on.
/// </remarks>
public class CsrfProtectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CsrfProtectionMiddleware> _logger;

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
        var method = context.Request.Method;
        if (!HttpMethods.IsPost(method) && !HttpMethods.IsPut(method) &&
            !HttpMethods.IsPatch(method) && !HttpMethods.IsDelete(method))
        {
            await _next(context);
            return;
        }

        var origin = context.Request.Headers.Origin.FirstOrDefault()
            ?? SiteAddressConfiguration.OriginOf(context.Request.Headers.Referer.FirstOrDefault());

        // No Origin and no Referer: not a browser, which attaches Origin to every
        // state-changing request. A cross-site post arrives without the Lax cookie.
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

            // Thrown, so ErrorHandlingMiddleware above shapes the body with its traceId.
            throw new HttpException(HttpStatusCode.Forbidden, RefusalMessage.ForeignOrigin);
        }

        await _next(context);
    }

    /// <summary>
    /// Whether an origin is one this site answers on.
    /// </summary>
    /// <remarks>
    /// Both sides go through the normaliser UseCors is handed, so one list means
    /// one policy.
    /// </remarks>
    private static bool IsOriginAllowed(string origin, IReadOnlyList<string> allowedOrigins) =>
        SiteAddressConfiguration.OriginOf(origin) is { } normalized &&
        allowedOrigins.Contains(normalized, StringComparer.Ordinal);
}
