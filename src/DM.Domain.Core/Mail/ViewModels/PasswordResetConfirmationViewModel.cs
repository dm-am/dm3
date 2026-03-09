namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for password reset confirmation letter
/// </summary>
/// <paramref name="Username">
/// Username
/// </paramref>
/// <paramref name="ConfirmationLinkUrl">
/// Confirmation link URL
/// </paramref>
public record PasswordResetConfirmationViewModel(
    string Username,
    string ConfirmationLinkUrl);
