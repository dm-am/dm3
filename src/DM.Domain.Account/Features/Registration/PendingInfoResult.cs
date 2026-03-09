namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Result of checking activation token status
/// </summary>
public class PendingInfoResult
{
    /// <summary>
    /// Token status: "ready" (can activate), "expired" (need resend), "not_found"
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Email associated with the pending registration (for UI display)
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Hint message for "not_found" status
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Create result for valid token
    /// </summary>
    public static PendingInfoResult Ready(string email) =>
        new() { Status = "ready", Email = email };

    /// <summary>
    /// Create result for expired token (pending exists but token is old)
    /// </summary>
    public static PendingInfoResult Expired(string email) =>
        new() { Status = "expired", Email = email };

    /// <summary>
    /// Create result for not found token
    /// </summary>
    public static PendingInfoResult NotFound(string hint) =>
        new() { Status = "not_found", Hint = hint };
}
