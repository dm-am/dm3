using System.Net;

namespace DM.Domain.Core.Exceptions;

/// <summary>
/// General HTTP exception
/// </summary>
public class HttpException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public HttpException(HttpStatusCode statusCode, string message) : base(message) => StatusCode = statusCode;

    /// <summary>
    /// What the refusal carries besides its sentence, written into the problem
    /// document as it is.
    /// </summary>
    /// <remarks>
    /// One refusal needs it today: a ban names the day it runs out, and the day
    /// is a value rather than a sentence, so the screen can spell it in its own
    /// words and in the reader's own time zone.
    /// </remarks>
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } =
        new Dictionary<string, object?>();
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

    /// <param name="errors">Key-value list of invalid client fields and errors</param>
    /// <param name="message">Client message</param>
    public HttpBadRequestException(IDictionary<string, string> errors,
        string message = RefusalMessage.InvalidData)
        : base(HttpStatusCode.BadRequest, message) => ValidationErrors = errors;
}

