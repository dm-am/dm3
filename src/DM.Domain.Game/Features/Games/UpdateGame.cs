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
    /// Visibility of draft content (when Status = Draft)
    /// </summary>
    public DraftVisibility? DraftVisibility { get; set; }

    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool? IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (null = unlimited)
    /// </summary>
    public int? RecruitmentPcLimit { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Game RPG system
    /// </summary>
    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, Warhammer, Our world)
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
