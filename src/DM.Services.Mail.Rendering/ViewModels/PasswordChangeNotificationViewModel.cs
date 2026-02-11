namespace DM.Services.Mail.Rendering.ViewModels;

/// <summary>
/// View model for password change notification letter
/// </summary>
/// <paramref name="Login">
/// User login
/// </paramref>
public record PasswordChangeNotificationViewModel(
    string Login);
