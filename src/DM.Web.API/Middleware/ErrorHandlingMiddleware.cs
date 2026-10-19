using System;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
using DM.Web.API.Shared.Http;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;

namespace DM.Web.API.Middleware;

/// <summary>
/// Middleware for exceptions handling
/// </summary>
internal class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext httpContext,
        ILogger<ErrorHandlingMiddleware> logger,
        IIdentityProvider identityProvider,
        ICorrelationTokenProvider correlationTokenProvider,
        ProblemDetailsFactory problemDetailsFactory)
    {
        try
        {
            await _next(httpContext);
        }
        catch (OperationCanceledException) when (httpContext.RequestAborted.IsCancellationRequested)
        {
            // The caller hung up: nobody to answer, and nothing went wrong.
            logger.LogDebug("Request aborted by the client: {Path}", httpContext.Request.Path);
        }
        catch (Exception e)
        {
            // Named explicitly: the "User" enricher pushed downstream is already popped.
            var user = identityProvider.Current?.User?.Username ?? "anonymous";

            // A started response cannot take a status, and the attempt would throw.
            if (httpContext.Response.HasStarted)
            {
                logger.LogCritical(e, "Unhandled server error after the response started for {User}", user);
                httpContext.Abort();
                return;
            }

            ProblemDetails error;
            switch (e)
            {
                case HttpBadRequestException badRequestException:
                    error = problemDetailsFactory.CreateFrom(badRequestException, httpContext);
                    break;
                case IntentionManagerException securityException:
                    logger.LogWarning(securityException, "Security breach attempt by {User}", user);
                    error = problemDetailsFactory.CreateFrom(securityException, httpContext);
                    break;
                case HttpException httpException:
                    error = problemDetailsFactory.CreateFrom(httpException, httpContext);
                    break;
                case ValidationException validationException:
                    error = problemDetailsFactory.CreateFrom(validationException, httpContext);
                    break;
                default:
                    logger.LogCritical(e, "Unhandled server error for {User}", user);
                    error = problemDetailsFactory.CreateForUnhandled(httpContext, correlationTokenProvider.Current);
                    break;
            }

            httpContext.Response.StatusCode = error.Status!.Value;
            // The contentType overload: WriteAsJsonAsync overwrites a ContentType set before it.
            await httpContext.Response.WriteAsJsonAsync(error, error.GetType(), options: null, ApiContentTypes.ProblemJson);
        }
    }
}
