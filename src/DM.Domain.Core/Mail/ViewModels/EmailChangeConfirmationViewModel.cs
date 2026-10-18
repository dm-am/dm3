namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for email change confirmation letter
/// </summary>
/// <paramref name="ConfirmationLinkUrl">
/// Link to confirm email change
/// </paramref>
public record EmailChangeConfirmationViewModel(
    string ConfirmationLinkUrl);
