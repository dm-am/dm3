using System;

namespace DM.Domain.Account.Features.PasswordChange;

/// <summary>
/// DTO model for password update
/// </summary>
public class UserPasswordChange
{
    /// <summary>
    /// Password reset token
    /// </summary>
    public Guid? Token { get; set; }

    public string OldPassword { get; set; } = null!;

    public string NewPassword { get; set; } = null!;
}
