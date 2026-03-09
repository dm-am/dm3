using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <summary>
/// DTO for new room access
/// </summary>
public class CreateRoomAccess
{
    /// <summary>
    /// Room identifier
    /// </summary>
    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Reader username
    /// </summary>
    public string ReaderUsername { get; set; } = null!;

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}