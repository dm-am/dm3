using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Standard API error format
/// </summary>
/// <remarks>
/// Error object for error envelope. Contains error code, human-readable message,
/// and optional field-specific validation details.
/// </remarks>
public class ApiError
{
    /// <summary>
    /// Creates a new API error
    /// </summary>
    /// <param name="code">Error code</param>
    /// <param name="message">Human-readable message</param>
    /// <param name="details">Field-specific validation errors</param>
    public ApiError(string code, string message, IDictionary<string, IEnumerable<string>>? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    /// <summary>
    /// Error code (machine-readable)
    /// </summary>
    /// <example>validation_error</example>
    public string Code { get; }

    /// <summary>
    /// Human-readable error message
    /// </summary>
    /// <example>Validation failed</example>
    public string Message { get; }

    /// <summary>
    /// Field-specific validation errors (optional)
    /// </summary>
    /// <remarks>
    /// Keys are field names, values are arrays of error messages for that field.
    /// Only present for validation errors (400).
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, IEnumerable<string>>? Details { get; }
}

/// <summary>
/// Standard error codes
/// </summary>
public static class ErrorCodes
{
    /// <summary>Field validation errors</summary>
    public const string ValidationError = "validation_error";

    /// <summary>Invalid request format</summary>
    public const string InvalidRequest = "invalid_request";

    /// <summary>Authentication required</summary>
    public const string Unauthorized = "unauthorized";

    /// <summary>Insufficient permissions</summary>
    public const string Forbidden = "forbidden";

    /// <summary>Resource not found</summary>
    public const string NotFound = "not_found";

    /// <summary>Resource already exists</summary>
    public const string Conflict = "conflict";

    /// <summary>Resource deleted or expired</summary>
    public const string Gone = "gone";

    /// <summary>Business logic prevents action</summary>
    public const string BusinessError = "business_error";

    /// <summary>Too many requests</summary>
    public const string RateLimited = "rate_limited";

    /// <summary>Internal server error</summary>
    public const string ServerError = "server_error";

    /// <summary>Feature not implemented</summary>
    public const string NotImplemented = "not_implemented";
}
