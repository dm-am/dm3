using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Input;

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