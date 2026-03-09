namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Error envelope matching the standard API response format
/// </summary>
/// <remarks>
/// When an error occurs, the envelope contains null resource and the error details.
/// This maintains consistency with successful responses that wrap resources.
/// </remarks>
public class ErrorEnvelope
{
    /// <summary>
    /// Creates an error envelope
    /// </summary>
    /// <param name="errors">Error details</param>
    public ErrorEnvelope(ApiError errors)
    {
        Errors = errors;
    }

    /// <summary>
    /// Always null for error responses
    /// </summary>
    public object? Resource => null;

    /// <summary>
    /// Error details
    /// </summary>
    public ApiError Errors { get; }
}
