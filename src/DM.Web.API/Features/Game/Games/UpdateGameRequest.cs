using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Content;
using CommentariesAccessMode = DM.Domain.Core.Enums.CommentsAccessMode;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO for editing an existing game
/// </summary>
/// <remarks>
/// Only what a master may edit, every field optional; the state changes through
/// POST /v1/games/{id}/status.
/// </remarks>
public class UpdateGameRequest
{
    /// <summary>
    /// Game title
    /// </summary>
    [StringLength(GameFieldLimits.TitleMaxLength, MinimumLength = GameFieldLimits.TitleMinLength,
        ErrorMessage = "Название до {1} символов")]
    public string? Title { get; set; }

    /// <summary>
    /// RPG system name
    /// </summary>
    [StringLength(GameFieldLimits.SystemMaxLength, ErrorMessage = "Название системы не длиннее {1} символов")]
    public string? System { get; set; }

    /// <summary>
    /// Narrative setting
    /// </summary>
    [StringLength(GameFieldLimits.SettingMaxLength, ErrorMessage = "Название сеттинга не длиннее {1} символов")]
    public string? Setting { get; set; }

    /// <summary>
    /// Game description/info (BB-code formatted)
    /// </summary>
    public string? Info { get; set; }

    /// <summary>
    /// Game tag short identifiers, the ones GET /games/tags serves. The whole
    /// set, not a delta: an omitted field leaves the tags alone, and an empty
    /// list clears them.
    /// </summary>
    public IEnumerable<int>? Tags { get; set; }

    /// <summary>
    /// Privacy settings. An omitted block leaves every setting unchanged.
    /// </summary>
    public UpdateGamePrivacySettings? PrivacySettings { get; set; }

    /// <summary>
    /// Recruitment settings. An omitted block leaves recruitment unchanged.
    /// </summary>
    public UpdateGameRecruitment? Recruitment { get; set; }
}

/// <summary>
/// Editable privacy settings of a game
/// </summary>
public class UpdateGamePrivacySettings
{
    /// <summary>
    /// Players can read private messages in posts
    /// </summary>
    public bool? ViewPrivates { get; set; }

    /// <summary>
    /// Players can see dice roll results
    /// </summary>
    public bool? ViewDice { get; set; }

    /// <summary>
    /// Players can see post statistics
    /// </summary>
    public bool? ViewPostStats { get; set; }

    /// <summary>
    /// Access mode for game commentaries
    /// </summary>
    public CommentariesAccessMode? CommentariesAccess { get; set; }
}

/// <summary>
/// Editable recruitment settings of a game
/// </summary>
/// <remarks>
/// Read-only recruitment facts — how many characters are in, when the current
/// wave started, whether it is a repeat wave — are computed by the server and
/// live on the response only.
/// </remarks>
public class UpdateGameRecruitment
{
    /// <summary>
    /// Recruitment is open for new players
    /// </summary>
    public bool? IsOpen { get; set; }

    /// <summary>
    /// Maximum number of player characters allowed (null = unlimited)
    /// </summary>
    public int? PcLimit { get; set; }
}
