using System;

namespace DM.Domain.Personal.Features.Blacklists;

/// <summary>
/// Blacklist entry DTO
/// </summary>
public class BlacklistEntry
{
    /// <summary>Entry identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Blocked user's display name</summary>
    public string Username { get; set; } = null!;

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Entity DTO for creating a blacklist entry (repository level)
/// </summary>
public class CreateBlacklistEntryEntity
{
    public Guid EntryId { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>
    /// Blocked user identifier
    /// </summary>
    public Guid BlockedUserId { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }
}
