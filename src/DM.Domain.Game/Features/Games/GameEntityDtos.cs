using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Games;

public class CreateGameEntity
{
    public Guid GameId { get; set; }

    public Guid MasterId { get; set; }

    public string Title { get; set; } = null!;

    public string? SystemName { get; set; }

    public string? NarrativeSetting { get; set; }

    public string? Info { get; set; }

    /// <summary>
    /// Draft or Active
    /// </summary>
    public ModuleStatus Status { get; set; }

    /// <summary>
    /// Approved for most masters, AwaitingEdits for a newbie or a master under moderation watch
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Null for drafts
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    public bool HideDiceResult { get; set; }

    public bool ShowPrivateMessages { get; set; }

    public bool HidePostStats { get; set; }

    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// Written in the same save as the game row: a game without a sheet is a defect, not a state
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> AttributeSpecifications { get; set; } = [];

    public bool IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Null for drafts
    /// </summary>
    public DateTimeOffset? RecruitmentStartedUtc { get; set; }

    /// <summary>
    /// Times recruitment has been opened, 1 for the first activation
    /// </summary>
    public int RecruitmentCount { get; set; }

    public IEnumerable<Guid> TagIds { get; set; } = [];

    /// <summary>
    /// Copied in the transaction of the game row: a game open without its master's list lets the people on it in
    /// </summary>
    public bool CopyMasterBlacklist { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// A null field is left unchanged
/// </summary>
public class UpdateGameEntity
{
    public Guid GameId { get; set; }

    public ModuleStatus? Status { get; set; }

    public PremoderationStatus? PremoderationStatus { get; set; }

    public ClosedReason? ClosedReason { get; set; }

    public bool? IsRecruitmentOpen { get; set; }

    /// <summary>
    /// Reopening recruitment
    /// </summary>
    public bool IncrementRecruitmentCount { get; set; }

    public int? RecruitmentPcLimit { get; set; }

    public string? Title { get; set; }

    public string? SystemName { get; set; }

    public string? NarrativeSetting { get; set; }

    public string? Info { get; set; }

    public bool? HideDiceResult { get; set; }

    public bool? ShowPrivateMessages { get; set; }

    public bool? HidePostStats { get; set; }

    public CommentsAccessMode? CommentsAccessMode { get; set; }

    public IEnumerable<Guid>? TagIds { get; set; }

    /// <summary>
    /// Set on first activation
    /// </summary>
    public DateTimeOffset? ActivatedUtc { get; set; }

    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Reopening
    /// </summary>
    public bool ClearClosedUtc { get; set; }

    /// <summary>
    /// Written only when <see cref="SetMentorId"/> is true, where null clears the curator
    /// </summary>
    public Guid? MentorId { get; set; }

    public bool SetMentorId { get; set; }

    /// <summary>
    /// The admin reset of the recruitment date
    /// </summary>
    public bool ClearRecruitmentStartedUtc { get; set; }
}

public class AddAssistantEntity
{
    /// <summary>
    /// Pre-generated GameAssistant link identifier
    /// </summary>
    public Guid GameAssistantId { get; set; }

    public Guid GameId { get; set; }

    public Guid UserId { get; set; }

    public DateTimeOffset JoinedUtc { get; set; }
}
