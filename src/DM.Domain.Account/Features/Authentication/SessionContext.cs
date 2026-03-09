namespace DM.Domain.Account.Features.Authentication;

/// <summary>
/// Context information for session creation
/// </summary>
public class SessionContext
{
    /// <summary>
    /// Client IP address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent header value
    /// </summary>
    public string? UserAgent { get; set; }
}
