#nullable enable
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for adding security headers to HTTP responses
/// </summary>
/// <remarks>
/// Adds essential security headers to protect against common vulnerabilities:
/// - X-Frame-Options: Prevents clickjacking attacks
/// - X-Content-Type-Options: Prevents MIME-type sniffing
/// - X-XSS-Protection: Enables XSS filter in older browsers
/// - Referrer-Policy: Controls referrer information
/// - Permissions-Policy: Restricts browser features
/// - Content-Security-Policy: Prevents XSS and data injection attacks
/// - Strict-Transport-Security: Enforces HTTPS (production only)
/// </remarks>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    /// <summary>
    /// Creates new security headers middleware
    /// </summary>
    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    /// <summary>
    /// Adds security headers to the response
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers before processing the request
        var headers = context.Response.Headers;

        // Prevent clickjacking by disallowing the page to be displayed in frames
        headers["X-Frame-Options"] = "DENY";

        // Prevent MIME-type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Enable XSS filter in older browsers (deprecated but harmless)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Control referrer information sent to other sites
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict browser features to prevent misuse
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // Content Security Policy: defense-in-depth against XSS and data injection
        // - default-src 'self': Only allow resources from same origin by default
        // - script-src 'self' 'unsafe-inline': Allow inline scripts (needed for some frameworks)
        // - style-src 'self' 'unsafe-inline': Allow inline styles (needed for component libraries)
        // - img-src 'self' data: https:: Allow images from same origin, data URIs, and HTTPS
        // - font-src 'self': Only allow fonts from same origin
        // - connect-src 'self' wss:: Allow AJAX/WebSocket connections to same origin and secure WebSockets
        // - frame-ancestors 'none': Prevent embedding in frames (complements X-Frame-Options)
        headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self' wss:; frame-ancestors 'none'";

        // HSTS: Force HTTPS for one year (production only, not on localhost)
        if (_environment.IsProduction() && !IsLocalhost(context.Request.Host))
        {
            headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context);
    }

    private static bool IsLocalhost(HostString host)
    {
        var hostValue = host.Host;
        return hostValue.Equals("localhost", System.StringComparison.OrdinalIgnoreCase) ||
               hostValue.Equals("127.0.0.1", System.StringComparison.OrdinalIgnoreCase) ||
               hostValue.Equals("::1", System.StringComparison.OrdinalIgnoreCase);
    }
}
