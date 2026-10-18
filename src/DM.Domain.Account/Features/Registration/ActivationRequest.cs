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
    /// The account keeps it for its letters, which have no browser to ask. One
    /// this machine does not know, or none, is left out and the account keeps UTC:
    /// a zone is no reason to refuse an account.
    /// </remarks>
    public string? TimeZoneId { get; set; }
}
