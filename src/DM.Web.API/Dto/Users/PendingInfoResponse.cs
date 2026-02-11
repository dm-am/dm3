namespace DM.Web.API.Dto.Users;

/// <summary>
/// Response for activation token status check
/// </summary>
public class PendingInfoResponse
{
    /// <summary>
    /// Token status: "ready" (can activate), "expired" (need resend)
    /// </summary>
    /// <example>ready</example>
    public required string Status { get; set; }

    /// <summary>
    /// Email associated with the pending registration (for UI display)
    /// </summary>
    /// <example>user@example.com</example>
    public required string Email { get; set; }
}
