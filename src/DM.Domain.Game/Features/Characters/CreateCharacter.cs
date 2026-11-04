using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;

public class CreateCharacter
{
    public Guid GameId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsNpc { get; set; }

    public CharacterAccessPolicy AccessPolicy { get; set; }

    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}
