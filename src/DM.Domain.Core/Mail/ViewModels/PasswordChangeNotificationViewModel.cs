namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for password change notification letter
/// </summary>
/// <paramref name="Username">
/// Username
/// </paramref>
public record PasswordChangeNotificationViewModel(
    string Username);
