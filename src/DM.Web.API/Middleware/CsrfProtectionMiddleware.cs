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
/// credentials to a cross-site request by itself: validating the origin of a
/// state-changing request is a primary CSRF control here, not defence in depth. It
/// works together with SameSite=Lax on the session cookie, which covers the requests
/// that carry no origin at all.
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

        if (!IsOriginAllowed(origin, settings.Value.CorsUrls))
        {
            _logger.LogWarning(
                "CSRF protection blocked request from origin {Origin}. Allowed: {AllowedOrigins}",
                origin, string.Join(", ", settings.Value.CorsUrls));

            // Тело отказа собирает ErrorHandlingMiddleware, и только оно: оно
            // стоит выше в конвейере, поэтому исключение отсюда до него дойдет.
            // Своя сборка ProblemDetails давала ответ без traceId и без type —
            // форму, которой нет ни у одного другого отказа. Токен корреляции
            // существует ровно для того, чтобы связать отказ с записью в логе,
            // и здесь его как раз не было.
            throw new HttpException(HttpStatusCode.Forbidden, "Запрос пришел с недопустимого адреса");
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
