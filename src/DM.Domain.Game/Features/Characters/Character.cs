using System;
using System.Collections.Generic;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Characters;

/// <summary>
/// Shortened character info
/// </summary>
public class CharacterShort
{
    public Guid Id { get; set; }

    /// <summary>
    /// Null for an NPC
    /// </summary>
    public GeneralUser? Author { get; set; }

    public CharacterStatus Status { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>
    /// Null URLs when no avatar is uploaded, as in <see cref="GeneralUser.Picture"/>
    /// </summary>
    public AvatarPicture Picture { get; set; } = new();

    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }
}

public class CharacterAttribute
{
    public Guid Id { get; set; }

    /// <summary>
    /// Attribute specification identifier
    /// </summary>
    public Guid AttributeId { get; set; }

    public string Title { get; set; } = null!;

    public string Value { get; set; } = null!;

    public int? Modifier { get; set; }

    /// <summary>
    /// Set by the value filler, so the API renders <see cref="AttributeSpecificationType.BbCode"/> values
    /// </summary>
    public AttributeSpecificationType Type { get; set; }

    /// <summary>
    /// Value is inconsistent with specification
    /// </summary>
    public bool Inconsistent { get; set; }
}

public class Character : CharacterShort
{
    public Guid GameId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public bool IsDead { get; set; }

    /// <summary>
    /// Player has voluntarily left the game
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player has been exiled from the game by master
    /// </summary>
    public bool IsPlayerExiled { get; set; }

    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Null if the character never posted
    /// </summary>
    public DateTimeOffset? LastPostUtc { get; set; }

    /// <summary>
    /// Value of the descriptor attribute, null when there is none or it is hidden from the viewer
    /// </summary>
    public string? Descriptor { get; set; }

    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}

/// <summary>
/// Short character info for game listing
/// </summary>
public class CharacterShortInfo : CharacterShort
{
    public int PostsCount { get; set; }
}

public class CharacterToUpdate
{
    public Guid Id { get; set; }

    public Guid GameId { get; set; }

    public Guid AuthorId { get; set; }

    public Guid GameMasterId { get; set; }

    public IEnumerable<Guid> GameAssistantIds { get; set; } = [];

    public ModuleStatus GameStatus { get; set; }

    public CharacterStatus Status { get; set; }

    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    public bool IsDead { get; set; }

    /// <summary>
    /// Player has voluntarily left the game
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// The lead has thrown the character out of the game
    /// </summary>
    public bool IsPlayerExiled { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}
