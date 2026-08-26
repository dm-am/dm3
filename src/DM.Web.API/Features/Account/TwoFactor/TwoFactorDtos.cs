using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Features.Account.TwoFactor;

/// <summary>
/// State of the second factor, as the settings screen reads it
/// </summary>
/// <remarks>
/// Nothing here can be used to pass the factor: no secret, no code, no hash. The
/// counts and the dates are what the owner needs in order to decide whether to
/// reissue the codes or to switch the factor off.
/// </remarks>
public class TwoFactorStatusResponse
{
    /// <summary>Whether the factor is on</summary>
    public bool Enabled { get; set; }

    /// <summary>Since when it has been on</summary>
    public DateTimeOffset? EnabledUtc { get; set; }

    /// <summary>Last time it was passed</summary>
    public DateTimeOffset? LastVerifiedUtc { get; set; }

    /// <summary>How many recovery codes are left unspent</summary>
    public int RecoveryCodesLeft { get; set; }

    /// <summary>When a removal requested by mail takes effect, if one is pending</summary>
    public DateTimeOffset? RemovalDueUtc { get; set; }

    /// <summary>Whether this account's rank owes a factor</summary>
    public bool Required { get; set; }

    /// <summary>Whether the rank is withheld for want of a factor</summary>
    public bool PrivilegeWithheld { get; set; }
}

/// <summary>
/// The secret, handed over once
/// </summary>
public class TwoFactorSetupResponse
{
    /// <summary>Base32 secret, for typing into the app by hand</summary>
    public string Secret { get; set; } = "";

    /// <summary>The same secret as an otpauth URI, for the QR code</summary>
    public string OtpAuthUri { get; set; } = "";
}

/// <summary>
/// A set of recovery codes, shown once
/// </summary>
public class RecoveryCodesResponse
{
    /// <summary>The codes. They cannot be read again from anywhere</summary>
    public IReadOnlyList<string> Codes { get; set; } = [];
}

/// <summary>
/// Ask for a secret to set the factor up with
/// </summary>
public class TwoFactorSetupRequest
{
    /// <summary>
    /// The account's current password
    /// </summary>
    /// <remarks>
    /// Not a formality. A session lives a year, and without this a stolen one
    /// would be enough to put somebody else's factor on the account.
    /// </remarks>
    [Required(ErrorMessage = "Введите пароль")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Пароль от 1 до 128 символов")]
    public string Password { get; set; } = "";
}

/// <summary>
/// Confirm the issued secret with the first code from the device
/// </summary>
public class TwoFactorConfirmRequest
{
    /// <summary>Code from the authenticator app</summary>
    [Required(ErrorMessage = "Введите код")]
    [StringLength(16, MinimumLength = 1, ErrorMessage = "Код от 1 до 16 символов")]
    public string Code { get; set; } = "";
}

/// <summary>
/// Switch the factor off, or reissue the recovery codes
/// </summary>
/// <remarks>
/// Both operations cost the same: the password and a passed second factor. A
/// recovery code counts as the second factor, or somebody who lost the device
/// could never switch the factor off at all.
/// </remarks>
public class TwoFactorConfirmedActionRequest
{
    /// <summary>The account's current password</summary>
    [Required(ErrorMessage = "Введите пароль")]
    [StringLength(128, MinimumLength = 1, ErrorMessage = "Пароль от 1 до 128 символов")]
    public string Password { get; set; } = "";

    /// <summary>Code from the device, or a recovery code</summary>
    [Required(ErrorMessage = "Введите код")]
    [StringLength(64, MinimumLength = 1, ErrorMessage = "Код от 1 до 64 символов")]
    public string Code { get; set; } = "";
}

/// <summary>
/// Ask, from the mailbox, for the factor to be taken off
/// </summary>
public class TwoFactorRemovalRequest
{
    /// <summary>Address the account answers at</summary>
    [Required(ErrorMessage = "Почта обязательна")]
    [EmailAddress(ErrorMessage = "Неверный формат почты")]
    public string Email { get; set; } = "";
}
