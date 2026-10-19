using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.RoomAccesses;

public class UpdateRoomAccess
{
    public Guid AccessId { get; set; }

    public RoomAccessPolicy Policy { get; set; }
}
