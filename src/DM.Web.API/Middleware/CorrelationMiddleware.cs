using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for correlation token provider
/// </summary>
internal class CorrelationMiddleware
{
    public const string CorrelationTokenHeader = "X-Dm-Correlation-Token";

    private readonly RequestDelegate _next;

    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ICorrelationTokenSetter setter,
        IGuidFactory guidFactory)
    {
        var correlationToken = httpContext.Request.Headers.TryGetValue(CorrelationTokenHeader, out var tokens) &&
                               Guid.TryParse(tokens.FirstOrDefault(), out var token)
            ? token
            : guidFactory.Create();
        setter.Current = correlationToken;

        // Trace and span ids are not pushed: the sink writes them from the event,
        // so a line inside a nested span names that span.
        await _next(httpContext);
    }
}
