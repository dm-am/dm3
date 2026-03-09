namespace DM.Web.API.Features.Account.Recovery;

/// <summary>
/// Response for password reset token status check
/// </summary>
public class PasswordResetTokenInfo
{
    /// <summary>
    /// Token status: "ready" (can change password), "expired" (need new reset request)
    /// </summary>
    /// <example>ready</example>
    public required string Status { get; set; }
}
