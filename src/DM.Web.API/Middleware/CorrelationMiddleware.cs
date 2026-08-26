using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for correlation token provider
/// </summary>
public class CorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private const string CorrelationTokenHeader = "X-Dm-Correlation-Token";

    /// <inheritdoc />
    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Before request
    /// </summary>
    /// <param name="httpContext"></param>
    /// <param name="setter"></param>
    /// <param name="guidFactory"></param>
    public async Task InvokeAsync(HttpContext httpContext,
        ICorrelationTokenSetter setter,
        IGuidFactory guidFactory)
    {
        var correlationToken = httpContext.Request.Headers.TryGetValue(CorrelationTokenHeader, out var tokens) &&
                               Guid.TryParse(tokens.FirstOrDefault(), out var token)
            ? token
            : guidFactory.Create();
        setter.Current = correlationToken;

        // The trace and span ids are not pushed here. The sink writes them out of
        // the event itself, so a copy pushed from the request would name the span
        // of the request on every line - including lines written inside a nested
        // span, which are the ones worth linking from.
        await _next(httpContext);
    }
}
