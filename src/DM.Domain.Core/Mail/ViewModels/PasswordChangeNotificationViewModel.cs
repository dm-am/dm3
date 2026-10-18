namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for password change notification letter
/// </summary>
/// <param name="SupportUrl">Address of the support page</param>
public record PasswordChangeNotificationViewModel(string SupportUrl);
