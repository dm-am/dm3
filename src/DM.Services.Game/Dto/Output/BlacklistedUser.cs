using System;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// Blacklisted user
/// </summary>
public class BlacklistedUser
{
    /// <summary>
    /// User identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Link identifier
    /// </summary>
    public Guid LinkId { get; set; }
}