using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using CommentariesAccessMode = DM.Domain.Core.Enums.CommentsAccessMode;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO for editing an existing game
/// </summary>
/// <remarks>
/// Only what a master may edit. The endpoint used to take GameDetails, the
/// 40-field response DTO, and the defence against the other 33 fields was a
/// column of Ignore() in the mapping profile — the arrangement under which a
/// field added to the response silently becomes writable, and the reviewer is
/// the only check. Status is absent by construction rather than by an Ignore:
/// a game changes state through POST /v1/games/{id}/status, which names the
/// transition and refuses an illegal one.
///
/// Every field is optional. PATCH sends what changed, and an omitted field
/// leaves the stored value alone.
/// </remarks>
public class UpdateGameRequest
{
    /// <summary>
    /// Game title
    /// </summary>
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Заголовок от 3 до 200 символов")]
    public string? Title { get; set; }

    /// <summary>
    /// RPG system name
    /// </summary>
    [StringLength(100, ErrorMessage = "Название системы не длиннее 100 символов")]
    public string? System { get; set; }

    /// <summary>
    /// Narrative setting
    /// </summary>
    [StringLength(100, ErrorMessage = "Название сеттинга не длиннее 100 символов")]
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
