using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.RoomAccesses;

public class CreateRoomAccessEntity
{
    public Guid AccessId { get; set; }

    public Guid RoomId { get; set; }

    public Guid? CharacterId { get; set; }

    public Guid? ReaderUserId { get; set; }

    public RoomAccessPolicy Policy { get; set; }
}

public class UpdateRoomAccessEntity
{
    public Guid AccessId { get; set; }

    public RoomAccessPolicy Policy { get; set; }
}
