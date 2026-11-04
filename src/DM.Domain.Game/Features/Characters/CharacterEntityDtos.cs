using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;

public class CharacterAttributeInput
{
    /// <summary>
    /// Attribute specification identifier
    /// </summary>
    public Guid Id { get; set; }

    public string Value { get; set; } = null!;
}

public class CreateCharacterEntity
{
    public Guid CharacterId { get; set; }

    public Guid GameId { get; set; }

    /// <summary>
    /// Null for an NPC
    /// </summary>
    public Guid? AuthorId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsNpc { get; set; }

    public CharacterAccessPolicy AccessPolicy { get; set; }

    public CharacterStatus InitialStatus { get; set; }

    public IEnumerable<CharacterAttributeInput> Attributes { get; set; } = [];

    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// A null field is left unchanged
/// </summary>
public class UpdateCharacterEntity
{
    public Guid CharacterId { get; set; }

    public CharacterStatus? Status { get; set; }

    public bool? IsDead { get; set; }

    public bool? IsPlayerLeft { get; set; }

    public bool? IsPlayerExiled { get; set; }

    public string? Name { get; set; }

    public bool? IsNpc { get; set; }

    public CharacterAccessPolicy? AccessPolicy { get; set; }

    public IEnumerable<CharacterAttributeInput>? Attributes { get; set; }
}
