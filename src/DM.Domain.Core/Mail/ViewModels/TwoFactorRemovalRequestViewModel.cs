namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for the letter that asks whether a removal of the second factor
/// was really requested
/// </summary>
/// <paramref name="Username">
/// Username
/// </paramref>
/// <paramref name="ConfirmationLinkUrl">
/// Link that schedules the removal
/// </paramref>
public record TwoFactorRemovalRequestViewModel(
    string Username,
    string ConfirmationLinkUrl);
