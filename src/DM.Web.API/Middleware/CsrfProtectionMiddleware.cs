#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for CSRF protection via Origin/Referer validation
/// </summary>
/// <remarks>
/// While the API uses custom auth headers (not cookies), this provides defense-in-depth
/// by validating that state-changing requests come from allowed origins.
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
    public async Task InvokeAsync(HttpContext context, IOptions<IntegrationSettings> settings)
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

        // If no origin/referer, this might be a direct API call (Postman, curl)
        // We allow these since they can't carry auth cookies anyway
        // Real browsers always send Origin for cross-origin requests
        if (string.IsNullOrEmpty(origin))
        {
            await _next(context);
            return;
        }

        if (!IsOriginAllowed(origin, settings.Value.CorsUrls))
        {
            _logger.LogWarning(
                "CSRF protection blocked request from origin {Origin}. Allowed: {AllowedOrigins}",
                origin, string.Join(", ", settings.Value.CorsUrls));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Invalid origin\"}");
            return;
        }

        await _next(context);
    }

    private static string? ExtractOriginFromReferer(string? referer)
    {
        if (string.IsNullOrEmpty(referer))
            return null;

        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            return null;

        return $"{uri.Scheme}://{uri.Host}{(uri.IsDefaultPort ? "" : $":{uri.Port}")}";
    }

    private static bool IsOriginAllowed(string origin, string[] allowedOrigins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
            return false;

        foreach (var allowed in allowedOrigins)
        {
            if (!Uri.TryCreate(allowed, UriKind.Absolute, out var allowedUri))
                continue;

            // Match host (case-insensitive) and port
            if (allowedUri.Host.Equals(originUri.Host, StringComparison.OrdinalIgnoreCase) &&
                allowedUri.Port == originUri.Port)
            {
                return true;
            }
        }

        return false;
    }
}
