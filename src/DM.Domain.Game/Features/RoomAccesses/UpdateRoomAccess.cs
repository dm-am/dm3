using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.RoomAccesses;

/// <summary>
/// DTO model for room access updating
/// </summary>
public class UpdateRoomAccess
{
    /// <summary>
    /// Access identifier
    /// </summary>
    public Guid AccessId { get; set; }

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}