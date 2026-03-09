using System;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Game.Users;

/// <summary>
/// Game user DTO
/// </summary>
public class GameUser
{
    /// <summary>
    /// User information
    /// </summary>
    public User User { get; set; } = null!;

    /// <summary>
    /// Role in the game: master, assistant, player, reader
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// When the user joined the game
    /// </summary>
    public DateTimeOffset JoinedUtc { get; set; }

    /// <summary>
    /// Character ID (for players only)
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Character name (for players only)
    /// </summary>
    public string? CharacterName { get; set; }

    /// <summary>
    /// Character status (for players only)
    /// </summary>
    public string? CharacterStatus { get; set; }
}
