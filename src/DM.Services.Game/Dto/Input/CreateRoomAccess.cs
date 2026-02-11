using System;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Input;

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
    /// Reader login
    /// </summary>
    public string ReaderLogin { get; set; } = null!;

    /// <summary>
    /// Access policy
    /// </summary>
    public RoomAccessPolicy Policy { get; set; }
}