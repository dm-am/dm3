using System;
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
        HttpException httpException, HttpContext httpContext) =>
        factory.CreateProblemDetails(httpContext, (int)httpException.StatusCode, httpException.Message);

    /// <summary>
    /// A refusal says only that it refused.
    /// </summary>
    /// <remarks>
    /// The message of this exception names the user, the intention and the target,
    /// which is what the log needs and none of what the caller should get: telling
    /// an anonymous caller which intention was evaluated against which entity
    /// makes the endpoint an oracle for entities they cannot read. The message is
    /// written to the log at Warning by the middleware before this runs.
    ///
    /// Same shape as the unhandled-exception overload below, and for the same
    /// reason — the only difference is that there the message is a framework one
    /// and here it is ours.
    /// </remarks>
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        IntentionManagerException intentionException, HttpContext httpContext) =>
        factory.CreateProblemDetails(httpContext, (int)intentionException.StatusCode,
            "Недостаточно прав для этого действия");

    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        HttpBadRequestException httpBadRequestException, HttpContext httpContext)
    {
        var modelStateDictionary = new ModelStateDictionary();
        foreach (var (key, value) in httpBadRequestException.ValidationErrors)
        {
            modelStateDictionary.AddModelError(key, value);
        }

        return factory.CreateValidationProblemDetails(httpContext,
            modelStateDictionary, StatusCodes.Status400BadRequest);
    }

    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        ValidationException validationException, HttpContext httpContext)
    {
        var modelStateDictionary = new ModelStateDictionary();
        foreach (var error in validationException.Errors)
        {
            modelStateDictionary.AddModelError(error.PropertyName, error.ErrorMessage);
        }

        return factory.CreateValidationProblemDetails(httpContext,
            modelStateDictionary, StatusCodes.Status400BadRequest, "Validation failed");
    }

    /// <summary>
    /// Unhandled exceptions are the ones nobody wrote a message for, so their
    /// message is a framework one: Npgsql names tables, columns and constraints,
    /// the S3 client names buckets, IO exceptions name server paths. The caller
    /// gets a constant title and the correlation token; the message itself is
    /// already written to the log at LogCritical.
    /// </summary>
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        Exception exception, HttpContext httpContext, Guid correlationId) =>
        factory.CreateProblemDetails(httpContext, StatusCodes.Status500InternalServerError, "Internal server error",
            detail: $"Server error. Address the administration for technical support. Use the following token to help us identify your issue: {correlationId}");
}