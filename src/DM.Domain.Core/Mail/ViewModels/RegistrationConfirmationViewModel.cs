namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for registration confirmation letter (email-first flow).
/// Username is chosen after email confirmation.
/// </summary>
/// <param name="ConfirmationLinkUrl">Link to activate the user and choose username</param>
public record RegistrationConfirmationViewModel(
    string ConfirmationLinkUrl);
