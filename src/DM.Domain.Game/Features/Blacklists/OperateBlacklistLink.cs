using System;

namespace DM.Domain.Game.Features.Blacklists;

public class OperateBlacklistLink
{
    public Guid GameId { get; set; }

    public string Username { get; set; } = null!;
}
