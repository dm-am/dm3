using System;

namespace DM.Domain.Game.Features.PostPendencies;

public class CreatePostPendencyEntity
{
    public Guid PendencyId { get; set; }

    public Guid RoomId { get; set; }

    public Guid CharacterId { get; set; }

    public Guid WaitingForUserId { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
