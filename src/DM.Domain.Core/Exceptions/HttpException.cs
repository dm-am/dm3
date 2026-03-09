using System;
using System.Collections.Generic;
using System.Net;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// General HTTP exception
/// </summary>
public class HttpException : Exception
{
    /// <summary>
    /// HTTP status code
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    /// New HTTP bypass exception
    /// </summary>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="message">Client message</param>
    public HttpException(HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode;
}

/// <summary>
/// General bad request HTTP exception
/// </summary>
public class HttpBadRequestException : HttpException
{
    /// <summary>
    /// Key-value list of invalid fields and validation errors
    /// </summary>
    public IDictionary<string, string> ValidationErrors { get; }

    /// <summary>
    /// New HTTP Bad Request exception
    /// </summary>
    /// <param name="errors">Key-value list of invalid client fields and errors</param>
    /// <param name="message">Client message</param>
    public HttpBadRequestException(IDictionary<string, string> errors,
        string message = "Invalid request parameters")
        : base(HttpStatusCode.BadRequest, message) => ValidationErrors = errors;
}

/// <summary>
/// HTTP exception with validation errors and custom status code
/// </summary>
public class HttpValidationException : HttpException
{
    /// <summary>
    /// Key-value list of invalid fields and validation errors
    /// </summary>
    public IDictionary<string, string> ValidationErrors { get; }

    /// <summary>
    /// New HTTP Validation exception with custom status code
    /// </summary>
    /// <param name="statusCode">HTTP status code (e.g., 401, 429)</param>
    /// <param name="errors">Key-value list of invalid client fields and errors</param>
    /// <param name="message">Client message</param>
    public HttpValidationException(HttpStatusCode statusCode, IDictionary<string, string> errors,
        string message = "Validation failed")
        : base(statusCode, message) => ValidationErrors = errors;
}
