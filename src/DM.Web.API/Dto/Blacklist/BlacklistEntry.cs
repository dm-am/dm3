using System;

namespace DM.Web.API.Dto.Blacklist;

/// <summary>
/// Blacklist entry DTO
/// </summary>
public class BlacklistEntry
{
    /// <summary>Entry identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Blocked user login</summary>
    public string Login { get; set; } = null!;

    /// <summary>Reason for blocking</summary>
    public string Reason { get; set; } = null!;

    /// <summary>Creation date</summary>
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// Request to block a user
/// </summary>
public class BlockUserRequest
{
    /// <summary>User login to block</summary>
    public string Login { get; set; } = null!;

    /// <summary>Optional reason for blocking</summary>
    public string Reason { get; set; } = null!;
}
