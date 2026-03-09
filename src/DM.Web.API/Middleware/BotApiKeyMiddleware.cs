using System.Threading.Tasks;
using DM.Infrastructure.Core.Configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware that validates X-Bot-Api-Key header for bot-only endpoints (/v1/bot/*)
/// </summary>
public class BotApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    /// <inheritdoc />
    public BotApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Invoke middleware
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IOptions<BotConfiguration> botConfig)
    {
        var path = context.Request.Path.Value;

        // Only apply to /v1/bot/* routes
        if (path != null && path.StartsWith("/v1/bot/"))
        {
            var apiKey = botConfig.Value.BotApiKey;

            // If no API key configured, allow all requests (dev mode)
            if (!string.IsNullOrEmpty(apiKey))
            {
                var providedKey = context.Request.Headers["X-Bot-Api-Key"].ToString();
                if (providedKey != apiKey)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Invalid or missing bot API key" });
                    return;
                }
            }
        }

        await _next(context);
    }
}
