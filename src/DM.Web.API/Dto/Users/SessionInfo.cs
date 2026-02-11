using System;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user session information
/// </summary>
public class SessionInfo
{
    /// <summary>
    /// Session identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Whether this is the current session
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Session persistence flag (remember me)
    /// </summary>
    public bool Persistent { get; set; }

    /// <summary>
    /// Session expiration date
    /// </summary>
    public DateTimeOffset ExpirationDate { get; set; }
}
