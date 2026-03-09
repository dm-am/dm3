using System;

namespace DM.Domain.Game.Features.Blacklists;

/// <summary>
/// DTO for blacklist link creating
/// </summary>
public class OperateBlacklistLink
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// User's display name (unique username)
    /// </summary>
    public string Username { get; set; } = null!;
}
