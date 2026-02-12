using System;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for password changing
/// </summary>
/// <remarks>
/// Two authentication methods are supported:
/// - Using OldPassword: User knows their current password
/// - Using Token: User received a password reset token via email
///
/// At least one of Token or OldPassword must be provided.
/// </remarks>
public class ChangePassword
{
    /// <summary>
    /// Password reset token (received via email)
    /// </summary>
    /// <remarks>
    /// Required when resetting password without knowing the old password.
    /// Either Token or OldPassword must be provided.
    /// </remarks>
    public Guid? Token { get; set; }

    /// <summary>
    /// Current password for verification
    /// </summary>
    /// <remarks>
    /// Required when changing password while logged in.
    /// Either Token or OldPassword must be provided.
    /// </remarks>
    [StringLength(128, ErrorMessage = "Old password must not exceed 128 characters")]
    public string? OldPassword { get; set; }

    /// <summary>
    /// New password (minimum 8 characters)
    /// </summary>
    [Required(ErrorMessage = "New password is required")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Password must be between 8 and 128 characters")]
    public string NewPassword { get; set; } = "";
}
