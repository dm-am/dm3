namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for password reset confirmation letter
/// </summary>
/// <param name="ConfirmationLinkUrl">
/// Confirmation link URL
/// </param>
/// <param name="LinkLifetimeHours">How long the link lives, as the server holds it</param>
public record PasswordResetConfirmationViewModel(
    string ConfirmationLinkUrl,
    int LinkLifetimeHours);
