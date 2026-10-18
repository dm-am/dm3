namespace DM.Domain.Core.Mail.ViewModels;

/// <summary>
/// View model for suspicious login notification letter
/// </summary>
/// <param name="Country">Country of the address, when a local table knows it</param>
/// <param name="IpAddress">IP address of the login</param>
/// <param name="DeviceInfo">Parsed device information</param>
/// <param name="Timestamp">When the login occurred</param>
public record SuspiciousLoginViewModel(
    string? IpAddress,
    string? DeviceInfo,
    string Timestamp,
    string? Country = null);
