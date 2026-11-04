using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.RoomAccesses;

public class CreateRoomAccess
{
    public Guid RoomId { get; set; }

    public Guid? CharacterId { get; set; }

    public string ReaderUsername { get; set; } = null!;

    public RoomAccessPolicy Policy { get; set; }
}
