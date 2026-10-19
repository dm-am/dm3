using System;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// DTO for completing activation with username selection
/// </summary>
public class ActivationRequest
{
    /// <summary>
    /// Activation token from email link
    /// </summary>
    public Guid Token { get; set; }

    /// <summary>
    /// Chosen username (unique display name)
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// IANA zone of the device the account is created on, or null
    /// </summary>
    /// <remarks>
    /// Kept for the letters; one this machine does not know is left out, not refused.
    /// </remarks>
    public string? TimeZoneId { get; set; }
}
