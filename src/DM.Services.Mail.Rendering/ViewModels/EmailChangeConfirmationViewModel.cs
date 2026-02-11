namespace DM.Services.Mail.Rendering.ViewModels;

/// <summary>
/// View model for email change confirmation letter
/// </summary>
/// <paramref name="Login">
/// User login
/// </paramref>
/// <paramref name="ConfirmationLinkUrl">
/// Link to confirm email change
/// </paramref>
public record EmailChangeConfirmationViewModel(
    string Login,
    string ConfirmationLinkUrl);
