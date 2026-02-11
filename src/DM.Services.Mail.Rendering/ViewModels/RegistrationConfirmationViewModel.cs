namespace DM.Services.Mail.Rendering.ViewModels;

/// <summary>
/// View model for registration confirmation letter (email-first flow).
/// Login is chosen after email confirmation.
/// </summary>
/// <param name="ConfirmationLinkUrl">Link to activate the user and choose login</param>
public record RegistrationConfirmationViewModel(
    string ConfirmationLinkUrl);
