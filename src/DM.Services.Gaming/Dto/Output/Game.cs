using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Gaming.Dto.Output;

/// <summary>
/// DTO model for game
/// </summary>
public class Game
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Created date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game status
    /// </summary>
    public GameStatus Status { get; set; }

    /// <summary>
    /// Premoderation status for newbie GMs
    /// </summary>
    public PremoderationStatus PremoderationStatus { get; set; }

    /// <summary>
    /// Game was completed successfully (only when Status = Closed)
    /// </summary>
    public bool IsFinished { get; set; }

    /// <summary>
    /// Game was frozen due to inactivity (only when Status = Closed)
    /// </summary>
    public bool IsFrozen { get; set; }

    /// <summary>
    /// Recruitment information
    /// </summary>
    public GameRecruitment Recruitment { get; set; }

    /// <summary>
    /// When the game was closed
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Game tags
    /// </summary>
    public IEnumerable<GameTag> Tags { get; set; }

    /// <summary>
    /// Date the game was first released
    /// </summary>
    public DateTimeOffset? ReleaseDate { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Game master
    /// </summary>
    public GeneralUser Master { get; set; }

    /// <summary>
    /// Game master's assistant
    /// </summary>
    public GeneralUser Assistant { get; set; }

    /// <summary>
    /// Game premoderation moderator
    /// </summary>
    public GeneralUser Mentor { get; set; }

    /// <summary>
    /// Pending assistant if any
    /// <remarks>Due to some issues in EFCore 3.1.5 had to move to IEnumerable, but make no mistake - that is a single assistant every time!</remarks>
    /// </summary>
    public GeneralUser PendingAssistant { get; set; }

    /// <summary>
    /// Active game character author ids
    /// </summary>
    public IEnumerable<Guid> ActiveCharacterUserIds { get; set; }

    /// <summary>
    /// Game reader ids
    /// </summary>
    public IEnumerable<Guid> ReaderUserIds { get; set; }

    /// <summary>
    /// Blacklisted user ids
    /// </summary>
    public IEnumerable<BlacklistedUser> BlacklistedUsers { get; set; }

    /// <summary>
    /// Game pending posts
    /// </summary>
    public IEnumerable<PendingPost> Pendings { get; set; }

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
    /// Commentaries access mode
    /// </summary>
    public CommentariesAccessMode CommentariesAccessMode { get; set; }

    /// <summary>
    /// Number of unread game posts
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Number of unread game commentaries
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Number of unread characters in game
    /// </summary>
    public int UnreadCharactersCount { get; set; }
}