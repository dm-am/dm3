namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for email change confirmation letter
/// </summary>
/// <paramref name="Username">
/// Username
/// </paramref>
/// <paramref name="ConfirmationLinkUrl">
/// Link to confirm email change
/// </paramref>
public record EmailChangeConfirmationViewModel(
    string Username,
    string ConfirmationLinkUrl);
