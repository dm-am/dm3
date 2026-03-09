using System;

namespace DM.Web.API.Features.Personal.Blacklists;

/// <summary>
/// Blacklist entry DTO
/// </summary>
public class BlacklistEntry
{
    /// <summary>Entry identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Blocked user's display name</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
