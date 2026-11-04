namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for email change confirmation letter
/// </summary>
/// <param name="ConfirmationLinkUrl">
/// Link to confirm email change
/// </param>
public record EmailChangeConfirmationViewModel(
    string ConfirmationLinkUrl);
