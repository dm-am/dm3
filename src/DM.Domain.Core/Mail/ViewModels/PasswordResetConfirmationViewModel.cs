namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for password reset confirmation letter
/// </summary>
/// <paramref name="ConfirmationLinkUrl">
/// Confirmation link URL
/// </paramref>
public record PasswordResetConfirmationViewModel(
    string ConfirmationLinkUrl);
