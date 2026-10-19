using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for adding security headers to HTTP responses
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Frame-Options"] = "DENY";
        headers["X-Content-Type-Options"] = "nosniff";
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // Directives that do not fall back to default-src are listed explicitly.
        // connect-src 'self' covers same-host wss; a bare wss: source would admit every host.
        // 'unsafe-inline' only in Development, for the boot script of Swagger UI.
        var scriptSrc = _environment.IsDevelopment()
            ? "script-src 'self' 'unsafe-inline'"
            : "script-src 'self'";

        headers["Content-Security-Policy"] = $"default-src 'self'; {scriptSrc}; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'; frame-src 'none'";

        if (_environment.IsProduction() && !IsLocalhost(context.Request.Host))
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }

    private static bool IsLocalhost(HostString host)
    {
        var hostValue = host.Host;
        return hostValue.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               hostValue.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               hostValue.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
