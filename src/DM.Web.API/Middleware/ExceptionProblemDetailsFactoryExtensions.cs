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
        HttpException httpException, HttpContext httpContext)
    {
        var problem = factory.CreateProblemDetails(
            httpContext, (int)httpException.StatusCode, httpException.Message);
        foreach (var (key, value) in httpException.Extensions)
        {
            problem.Extensions[key] = value;
        }

        return problem;
    }

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
            RefusalMessage.AccessDenied);

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
            modelStateDictionary, StatusCodes.Status400BadRequest, RefusalMessage.InvalidData);
    }

    /// <summary>
    /// Unhandled exceptions are the ones nobody wrote a message for, so their
    /// message is a framework one: Npgsql names tables, columns and constraints,
    /// the S3 client names buckets, IO exceptions name server paths. The caller
    /// gets a constant title and the correlation token; the message itself is
    /// already written to the log at LogCritical.
    /// </summary>
    /// <remarks>
    /// The title is the sentence the reader sees: forms relay it into their own
    /// strip and the response interceptor raises it as a toast. So it is copy
    /// and not a label, one sentence with no period, and the client holds the
    /// same words for the 5xx this middleware never sees, because an edge that
    /// refuses a request answers with no document at all. The two copies are
    /// held equal by generalRefusal.spec.ts on the client side.
    ///
    /// No call site reads the detail. It carries the token support asks for and
    /// no longer opens by repeating the title above it: one event said twice in
    /// one document is where a second wording of it starts.
    /// </remarks>
    public static ProblemDetails CreateFrom(this ProblemDetailsFactory factory,
        Exception exception, HttpContext httpContext, Guid correlationId) =>
        factory.CreateProblemDetails(httpContext, StatusCodes.Status500InternalServerError,
            "Ошибка сервера",
            detail: $"Обратитесь в поддержку и назовите этот токен: {correlationId}");
}
