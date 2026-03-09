using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

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
    public ModuleStatus? Status { get; set; }

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
    /// Game assistant username
    /// </summary>
    public string AssistantUsername { get; set; } = null!;

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
    /// Hide posts count and last post date for all users (except Master/Assistant)
    /// </summary>
    public bool? HidePostStats { get; set; }

    /// <summary>
    /// comments access mode
    /// </summary>
    public CommentsAccessMode? CommentsAccessMode { get; set; }

    /// <summary>
    /// Game tag identifiers
    /// </summary>
    public IEnumerable<Guid> Tags { get; set; } = [];

    #region Internal fields (set by service)

    /// <summary>
    /// Release date (set on first activation, internal)
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Closed date (set when closing, internal)
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Mentor user ID (set during premoderation, internal)
    /// </summary>
    public Guid? MentorId { get; set; }

    /// <summary>
    /// Recruitment started date (set when opening recruitment, internal)
    /// </summary>
    public DateTimeOffset? RecruitmentStartedUtc { get; set; }

    /// <summary>
    /// Soft delete flag (internal)
    /// </summary>
    public bool? IsRemoved { get; set; }

    #endregion
}