namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for username change rejection letter
/// </summary>
/// <param name="Username">Current username</param>
/// <param name="Reason">
/// Moderator's comment. Absent when the request was rejected without one.
/// </param>
public record UsernameChangeRejectionViewModel(
    string Username,
    string? Reason);
