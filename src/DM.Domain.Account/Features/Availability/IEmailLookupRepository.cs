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
}
