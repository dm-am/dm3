using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Gaming.Dto.Input;

/// <summary>
/// DTO model for game update
/// </summary>
public class UpdateGame
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Game status
    /// </summary>
    public GameStatus? Status { get; set; }

    /// <summary>
    /// Premoderation status
    /// </summary>
    public PremoderationStatus? PremoderationStatus { get; set; }

    /// <summary>
    /// Game was completed successfully (only when closing)
    /// </summary>
    public bool? IsFinished { get; set; }

    /// <summary>
    /// Game was frozen due to inactivity (only when closing)
    /// </summary>
    public bool? IsFrozen { get; set; }

    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool? IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Maximum number of players allowed (null = unlimited)
    /// </summary>
    public int? RecruitmentPlayerLimit { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Game RPG system
    /// </summary>
    public string SystemName { get; set; }

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, WarHammer, Our world)
    /// </summary>
    public string NarrativeSetting { get; set; }

    /// <summary>
    /// Game public information
    /// </summary>
    public string Info { get; set; }

    /// <summary>
    /// Game assistant login
    /// </summary>
    public string AssistantLogin { get; set; }

    /// <summary>
    /// Only GM and character author can see character temper
    /// </summary>
    public bool? HideTemper { get; set; }

    /// <summary>
    /// Only GM and character author can see character skills
    /// </summary>
    public bool? HideSkills { get; set; }

    /// <summary>
    /// Only GM and character author can see character inventory
    /// </summary>
    public bool? HideInventory { get; set; }

    /// <summary>
    /// Only GM and character author can see character story
    /// </summary>
    public bool? HideStory { get; set; }

    /// <summary>
    /// Characters has no alignment
    /// </summary>
    public bool? DisableAlignment { get; set; }

    /// <summary>
    /// Only GM and post author can see dice roll result
    /// </summary>
    public bool? HideDiceResult { get; set; }

    /// <summary>
    /// Everyone can read each others private messages
    /// </summary>
    public bool? ShowPrivateMessages { get; set; }

    /// <summary>
    /// Commentaries access mode
    /// </summary>
    public CommentariesAccessMode? CommentariesAccessMode { get; set; }

    /// <summary>
    /// Game notes
    /// </summary>
    public string Notepad { get; set; }

    /// <summary>
    /// Game tag identifiers
    /// </summary>
    public IEnumerable<Guid> Tags { get; set; }
}