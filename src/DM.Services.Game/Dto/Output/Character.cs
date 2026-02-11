using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Output;

/// <summary>
/// DTO model for game character
/// </summary>
public class Character
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Created date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification date (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Total characters posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Character author
    /// </summary>
    public GeneralUser Author { get; set; } = null!;

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character race
    /// </summary>
    public string Race { get; set; } = null!;

    /// <summary>
    /// Character class
    /// </summary>
    public string Class { get; set; } = null!;

    /// <summary>
    /// Character picture URL
    /// </summary>
    public string PictureUrl { get; set; } = null!;

    /// <summary>
    /// Character appearance
    /// </summary>
    public string Appearance { get; set; } = null!;

    /// <summary>
    /// Character temper
    /// </summary>
    public string Temper { get; set; } = null!;

    /// <summary>
    /// Character story
    /// </summary>
    public string Story { get; set; } = null!;

    /// <summary>
    /// Character skills
    /// </summary>
    public string Skills { get; set; } = null!;

    /// <summary>
    /// Character inventory
    /// </summary>
    public string Inventory { get; set; } = null!;

    /// <summary>
    /// Character alignment
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Character is NPC (non-player's character)
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    /// <summary>
    /// Character attribute
    /// </summary>
    public IEnumerable<CharacterAttribute> Attributes { get; set; } = [];
}