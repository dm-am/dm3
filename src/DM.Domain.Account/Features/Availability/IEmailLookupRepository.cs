using System;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Account.Features.Availability;

/// <summary>
/// Repository for private user lookup operations (email-based)
/// </summary>
public interface IEmailLookupRepository
{
    /// <summary>
    /// Get user info by email (private lookup for account operations)
    /// </summary>
    Task<EmailLookupInfo?> GetUserByEmail(string email, CancellationToken ct = default);

    /// <summary>
    /// Check if email exists
    /// </summary>
    Task<bool> EmailExists(string email, CancellationToken ct = default);
}

/// <summary>
/// Private user info for account operations
/// </summary>
public class EmailLookupInfo
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Username
    /// </summary>
    public string Username { get; set; } = null!;

    /// <summary>
    /// User email
    /// </summary>
    public string Email { get; set; } = null!;

    /// <summary>
    /// Whether the account behind the address has been removed
    /// </summary>
    /// <remarks>
    /// Found on purpose: recovery answers a removed account exactly as a live
    /// one on the wire and tells the truth in the letter, and it cannot do
    /// either without knowing which it found (SECURITY.md).
    /// </remarks>
    public bool IsRemoved { get; set; }
}
