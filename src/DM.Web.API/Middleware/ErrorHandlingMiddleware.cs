using System;
using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Abstractions;
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
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly RequestDelegate _next;

    /// <inheritdoc />
    public ErrorHandlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Before request
    /// </summary>
    /// <param name="httpContext">HTTP context</param>
    /// <param name="logger">Logger</param>
    /// <param name="identityProvider">Caller identity, named explicitly in the two log calls below</param>
    /// <param name="correlationTokenProvider">Correlation token for support assistance</param>
    /// <param name="problemDetailsFactory">Problem details factory</param>
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
            // The caller hung up — their 30-second timeout fired, or they closed the
            // tab. There is no one to answer and nothing went wrong, so this is not
            // an error: writing a body into a dead socket throws again, and letting
            // it fall through to the default branch below files a LogCritical for
            // every dropped connection.
            logger.LogDebug("Request aborted by the client: {Path}", httpContext.Request.Path);
        }
        catch (Exception e)
        {
            // This middleware wraps authentication, so by the time an exception
            // reaches it the request-scoped "User" enricher pushed downstream has
            // already been popped by the unwinding stack. The two log calls that
            // care who the caller was therefore name them as a parameter instead
            // of relying on ambient context.
            var user = identityProvider.Current?.User?.Username ?? "anonymous";
            object error;
            switch (e)
            {
                case HttpBadRequestException badRequestException:
                    error = problemDetailsFactory.CreateFrom(badRequestException, httpContext);
                    break;
                case IntentionManagerException securityException:
                    logger.LogWarning(securityException,
                        "Security breach attempt by {User}: {Message}", user, e.Message);
                    error = problemDetailsFactory.CreateFrom(securityException, httpContext);
                    break;
                case HttpException httpException:
                    error = problemDetailsFactory.CreateFrom(httpException, httpContext);
                    break;
                case ValidationException validationException:
                    error = problemDetailsFactory.CreateFrom(validationException, httpContext);
                    break;
                default:
                    logger.LogCritical(e, "Unhandled server error for {User}: {Message}", user, e.Message);
                    error = problemDetailsFactory.CreateFrom(e, httpContext, correlationTokenProvider.Current);
                    break;
            }

            httpContext.Response.StatusCode = error is ProblemDetails { Status: not null } problemDetails
                ? problemDetails.Status.Value
                : StatusCodes.Status500InternalServerError;
            // Через перегрузку с contentType: присваивание ContentType до
            // WriteAsJsonAsync перетиралось им на application/json, и клиент не мог
            // отличить ошибку по типу содержимого.
            await httpContext.Response.WriteAsJsonAsync(error, error.GetType(), options: null, ProblemJsonContentType);
        }
    }
}