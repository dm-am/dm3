using System;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

public class GameUser
{
    public GeneralUser User { get; set; } = null!;

    public GameRole Role { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }

    /// <summary>
    /// For players
    /// </summary>
    public Guid? CharacterId { get; set; }

    public string? CharacterName { get; set; }

    public CharacterStatus? CharacterStatus { get; set; }
}
