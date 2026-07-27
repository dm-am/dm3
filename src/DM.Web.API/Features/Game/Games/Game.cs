using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using CommentariesAccessMode = DM.Domain.Core.Enums.CommentsAccessMode;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// Current user participation in game
/// </summary>
[Flags]
public enum GameParticipation
{
    /// <summary>
    /// No participation
    /// </summary>
    None = 0,

    /// <summary>
    /// User is game owner (master)
    /// </summary>
    Owner = 1,

    /// <summary>
    /// User has authority (master or assistant)
    /// </summary>
    Authority = 2,

    /// <summary>
    /// User is pending assistant
    /// </summary>
    PendingAssistant = 4,

    /// <summary>
    /// User is an active player
    /// </summary>
    Player = 8,

    /// <summary>
    /// User is a reader/subscriber
    /// </summary>
    Reader = 16,

    /// <summary>
    /// User is a moderator/mentor
    /// </summary>
    Moderator = 32
}

/// <summary>
/// API DTO model for game (for lists and tables)
/// </summary>
/// <remarks>
/// Extends GameRef with additional fields for display in tables/cards.
/// Inherits: Id, Title, Status, ClosedReason, ActivatedUtc, Master, Assistants,
///           Participation, SubscribersCount, Recruitment, UnreadPostsCount, UnreadCommentsCount
/// </remarks>
public class Game : GameRef
{

    /// <summary>
    /// RPG system name
    /// </summary>
    public string System { get; set; } = null!;

    /// <summary>
    /// RPG setting name
    /// </summary>
    public string Setting { get; set; } = null!;

    /// <summary>
    /// Attribute schema identifier (for referencing, full schema in GameDetails)
    /// </summary>
    public Guid? SchemaId { get; set; }

    /// <summary>
    /// Game closed date (when status changed to Closed)
    /// </summary>
    public DateTimeOffset? ClosedUtc { get; set; }

    /// <summary>
    /// Game creation date (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Responsible for premoderation (lightweight reference)
    /// </summary>
    public UserRef? Mentor { get; set; }

    /// <summary>
    /// Game master's pending assistant (lightweight reference)
    /// </summary>
    public UserRef? PendingAssistant { get; set; }

    /// <summary>
    /// Game tags (full objects - only for single game details, null for lists)
    /// </summary>
    public IEnumerable<Tag>? Tags { get; set; }

    /// <summary>
    /// Tag IDs only (lightweight - for lists, uses cached tags for description lookup)
    /// </summary>
    public IEnumerable<int> TagIds { get; set; } = [];

    /// <summary>
    /// Number of unread characters
    /// </summary>
    public int UnreadCharactersCount { get; set; }

    /// <summary>
    /// Unique players (authors of active characters) - for game details page.
    /// </summary>
    public IEnumerable<UserRef>? Players { get; set; }

    /// <summary>
    /// Characters of the player targeted by the playerUsername query filter
    /// (name + status for the profile games table). Conditional: populated
    /// only on list responses where the playerUsername filter is set;
    /// null (omitted from JSON) otherwise.
    /// </summary>
    public IEnumerable<PlayerCharacterInfo>? PlayerCharacters { get; set; }
}

/// <summary>
/// Character info of the player targeted by the playerUsername filter
/// </summary>
public class PlayerCharacterInfo
{
    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Character died in game (meaningful when Status = Retired)
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player voluntarily left the game (meaningful when Status = Retired)
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player was exiled by the master (meaningful when Status = Retired)
    /// </summary>
    public bool IsPlayerExiled { get; set; }
}

/// <summary>
/// DTO for game recruitment information
/// </summary>
public class GameRecruitment
{
    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool IsOpen { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (null = unlimited)
    /// </summary>
    public int? PcLimit { get; set; }

    /// <summary>
    /// Current number of active player characters
    /// </summary>
    public int PcCount { get; set; }

    /// <summary>
    /// When the recruitment was started
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }

    /// <summary>
    /// Whether this is a subsequent recruitment ("донабор", RecruitmentCount >= 2)
    /// </summary>
    public bool IsSubsequent { get; set; }
}

/// <summary>
/// DTO model for game privacy settings
/// </summary>
public class GamePrivacySettings
{
    /// <summary>
    /// User can read other players private messages in in-game posts
    /// </summary>
    public bool? ViewPrivates { get; set; }

    /// <summary>
    /// User can see other players dice rolls and results
    /// </summary>
    public bool? ViewDice { get; set; }

    /// <summary>
    /// User can see post statistics (post count, last post date) for participants
    /// </summary>
    public bool? ViewPostStats { get; set; }

    /// <summary>
    /// Access mode to the game commentaries
    /// </summary>
    public CommentariesAccessMode? CommentariesAccess { get; set; }
}
