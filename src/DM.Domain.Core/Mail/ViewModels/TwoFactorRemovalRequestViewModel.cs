namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for the letter that asks whether a removal of the second factor
/// was really requested
/// </summary>
/// <param name="ConfirmationLinkUrl">
/// Link that schedules the removal
/// </param>
public record TwoFactorRemovalRequestViewModel(
    string ConfirmationLinkUrl);
