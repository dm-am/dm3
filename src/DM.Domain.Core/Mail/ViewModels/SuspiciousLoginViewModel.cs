namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for suspicious login notification letter
/// </summary>
/// <param name="Username">Username</param>
/// <param name="IpAddress">IP address of the login</param>
/// <param name="DeviceInfo">Parsed device information</param>
/// <param name="Timestamp">When the login occurred</param>
public record SuspiciousLoginViewModel(
    string Username,
    string? IpAddress,
    string? DeviceInfo,
    string Timestamp);
