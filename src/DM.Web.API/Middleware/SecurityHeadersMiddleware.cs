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
        // - connect-src 'self': Allow AJAX and WebSocket connections to this origin;
        //   'self' already covers wss:// on the same host, while a bare wss: source
        //   matches every host there is and bounds nothing
        // - frame-ancestors 'none': Prevent embedding in frames (complements X-Frame-Options)
        // form-action, base-uri, object-src and frame-src are listed explicitly:
        // none of them falls back to default-src, so leaving them out means they
        // are simply unrestricted (flagged by ZAP rule 10055).
        // Swagger UI is the only document this origin ever serves, it inlines its
        // own boot script, and it is mapped in Development alone — so that is the
        // only environment paying for 'unsafe-inline'. Everywhere else the API
        // answers JSON, and a document able to run an inline script from here is
        // the incident rather than the feature.
        var scriptSrc = _environment.IsDevelopment()
            ? "script-src 'self' 'unsafe-inline'"
            : "script-src 'self'";

        headers["Content-Security-Policy"] = $"default-src 'self'; {scriptSrc}; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self'; connect-src 'self'; frame-ancestors 'none'; form-action 'self'; base-uri 'self'; object-src 'none'; frame-src 'none'";

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
