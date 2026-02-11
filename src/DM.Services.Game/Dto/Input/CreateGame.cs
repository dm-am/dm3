using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Game.Dto.Input;

/// <summary>
/// DTO model for new game
/// </summary>
public class CreateGame
{
    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Game RPG system
    /// </summary>
    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, WarHammer, Our world)
    /// </summary>
    public string NarrativeSetting { get; set; } = null!;

    /// <summary>
    /// Game public information
    /// </summary>
    public string Info { get; set; } = null!;

    /// <summary>
    /// Game assistant login
    /// </summary>
    public string AssistantLogin { get; set; } = null!;

    /// <summary>
    /// Only GM and character author can see character temper
    /// </summary>
    public bool HideTemper { get; set; }

    /// <summary>
    /// Only GM and character author can see character skills
    /// </summary>
    public bool HideSkills { get; set; }

    /// <summary>
    /// Only GM and character author can see character inventory
    /// </summary>
    public bool HideInventory { get; set; }

    /// <summary>
    /// Only GM and character author can see character story
    /// </summary>
    public bool HideStory { get; set; }

    /// <summary>
    /// Characters has no alignment
    /// </summary>
    public bool DisableAlignment { get; set; }

    /// <summary>
    /// Only GM and post author can see dice roll result
    /// </summary>
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Everyone can read each others private messages
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date for all participants (except Master/Assistant)
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// Commentaries access mode
    /// </summary>
    public CommentariesAccessMode CommentariesAccessMode { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Game tag identifiers
    /// </summary>
    public IEnumerable<Guid> Tags { get; set; } = [];

    /// <summary>
    /// Create game as a draft
    /// </summary>
    public bool Draft { get; set; }
}