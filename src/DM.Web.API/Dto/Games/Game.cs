using System;
using System.Collections.Generic;
using DM.Services.Core.Dto.Enums;
using DM.Services.Gaming.Dto;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// API DTO model for game (lightweight, for lists)
/// </summary>
public class Game
{
    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Game title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// RPG system name
    /// </summary>
    public string System { get; set; }

    /// <summary>
    /// RPG setting name
    /// </summary>
    public string Setting { get; set; }

    /// <summary>
    /// Attribute schema identifier (for referencing, full schema in GameDetails)
    /// </summary>
    public Guid? SchemaId { get; set; }

    /// <summary>
    /// Game status
    /// </summary>
    public GameStatus? Status { get; set; }

    /// <summary>
    /// Game first release date
    /// </summary>
    public DateTimeOffset? Released { get; set; }

    /// <summary>
    /// Game master
    /// </summary>
    public User Master { get; set; }

    /// <summary>
    /// Game master's assistant
    /// </summary>
    public User Assistant { get; set; }

    /// <summary>
    /// Responsible for premoderation
    /// </summary>
    public User Mentor { get; set; }

    /// <summary>
    /// Game master's pending assistant
    /// </summary>
    public User PendingAssistant { get; set; }

    /// <summary>
    /// Requesting user participates in game
    /// </summary>
    public IEnumerable<GameParticipation> Participation { get; set; }

    /// <summary>
    /// Game tags
    /// </summary>
    public IEnumerable<Tag> Tags { get; set; }

    /// <summary>
    /// Number of unread posts
    /// </summary>
    public int UnreadPostsCount { get; set; }

    /// <summary>
    /// Number of unread commentaries
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Number of unread characters
    /// </summary>
    public int UnreadCharactersCount { get; set; }

    /// <summary>
    /// User IDs of active character owners (for player count in sidebar)
    /// </summary>
    public IEnumerable<Guid> ActiveCharacterUserIds { get; set; }

    /// <summary>
    /// Recruitment information
    /// </summary>
    public GameRecruitment Recruitment { get; set; }
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
    /// Maximum number of players allowed (null = unlimited)
    /// </summary>
    public int? PlayerLimit { get; set; }

    /// <summary>
    /// Current number of active players
    /// </summary>
    public int PlayerCount { get; set; }

    /// <summary>
    /// When the recruitment was started
    /// </summary>
    public DateTimeOffset? StartedUtc { get; set; }
}

/// <summary>
/// DTO model for game privacy settings
/// </summary>
public class GamePrivacySettings
{
    /// <summary>
    /// User can read characters temper
    /// </summary>
    public bool? ViewTemper { get; set; }

    /// <summary>
    /// User can read characters story
    /// </summary>
    public bool? ViewStory { get; set; }

    /// <summary>
    /// User can read characters skills
    /// </summary>
    public bool? ViewSkills { get; set; }

    /// <summary>
    /// User can read characters inventory
    /// </summary>
    public bool? ViewInventory { get; set; }

    /// <summary>
    /// User can read other players private messages in in-game posts
    /// </summary>
    public bool? ViewPrivates { get; set; }

    /// <summary>
    /// User can see other players dice rolls and results
    /// </summary>
    public bool? ViewDice { get; set; }

    /// <summary>
    /// Access mode to the game commentaries
    /// </summary>
    public CommentariesAccessMode? CommentariesAccess { get; set; }
}