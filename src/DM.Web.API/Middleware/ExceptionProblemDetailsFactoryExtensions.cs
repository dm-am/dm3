using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Core.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DM.Web.API.Middleware;

internal static class ExceptionProblemDetailsFactoryExtensions
{
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        HttpException httpException, HttpContext httpContext)
    {
        var problem = factory.CreateProblemDetails(
            httpContext, (int)httpException.StatusCode, httpException.Message);
        foreach (var (key, value) in httpException.Extensions)
        {
            problem.Extensions[key] = value;
        }

        // A permission check written by hand is the same refusal as the intention
        // one below, and is marked the same way.
        return httpException.Message == RefusalMessage.AccessDenied ? problem.General() : problem;
    }

    /// <summary>
    /// A refusal says only that it refused.
    /// </summary>
    /// <remarks>
    /// The message names the user, the intention and the target: the log gets it,
    /// the caller does not, or the endpoint answers which entities exist.
    /// </remarks>
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        IntentionManagerException intentionException, HttpContext httpContext) =>
        factory.CreateProblemDetails(httpContext, (int)intentionException.StatusCode,
            RefusalMessage.AccessDenied).General();

    /// <remarks>
    /// The message is the title, as for every other refusal: without it the
    /// framework titled the document in English.
    /// </remarks>
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        HttpBadRequestException httpBadRequestException, HttpContext httpContext) =>
        factory.Invalid(httpContext, httpBadRequestException.ValidationErrors,
            httpBadRequestException.Message);

    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        ValidationException validationException, HttpContext httpContext) =>
        factory.Invalid(httpContext,
            validationException.Errors.Select(error => KeyValuePair.Create(error.PropertyName, error.ErrorMessage)),
            RefusalMessage.InvalidData);

    /// <summary>
    /// Unhandled exceptions carry framework messages that name tables, buckets and
    /// paths, so the caller gets a constant title and the correlation token, and
    /// the exception is not a parameter here.
    /// </summary>
    /// <remarks>
    /// The title is copy the reader sees. The client holds the same words for a 5xx
    /// with no body, and generalRefusal.spec.ts keeps the two equal.
    /// </remarks>
    public static ProblemDetails CreateForUnhandled(this ProblemDetailsFactory factory,
        HttpContext httpContext, Guid correlationId) =>
        factory.CreateProblemDetails(httpContext, StatusCodes.Status500InternalServerError,
            "Ошибка сервера",
            detail: $"Обратитесь в поддержку и назовите этот код: {correlationId}").General();

    /// <summary>
    /// Marks a refusal that does not name its reason: the access check and the
    /// unhandled exception. The client tells such a refusal by the mark, not by
    /// the words of its title, and names the failed call before it.
    /// </summary>
    private static ProblemDetails General(this ProblemDetails problem)
    {
        problem.Extensions[GeneralMark] = true;
        return problem;
    }

    /// <summary>The extension of a refusal that names no reason of its own</summary>
    internal const string GeneralMark = "general";

    private static ValidationProblemDetails Invalid(this ProblemDetailsFactory factory, HttpContext httpContext,
        IEnumerable<KeyValuePair<string, string>> errors, string? title = null)
    {
        var modelStateDictionary = new ModelStateDictionary();
        foreach (var (key, message) in errors)
        {
            modelStateDictionary.AddModelError(key, message);
        }

        return factory.CreateValidationProblemDetails(httpContext,
            modelStateDictionary, StatusCodes.Status400BadRequest, title);
    }
}
