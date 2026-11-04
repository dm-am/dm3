using System;

namespace DM.Domain.Game.Features.Blacklists;

public class BlacklistedUser
{
    public Guid UserId { get; set; }

    public Guid LinkId { get; set; }
}
