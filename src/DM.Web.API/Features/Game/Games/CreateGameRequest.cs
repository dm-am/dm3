using System;
using DM.Domain.Core.Content;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DM.Domain.Core.Enums;
using CommentariesAccessMode = DM.Domain.Core.Enums.CommentsAccessMode;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO for creating a new game
/// </summary>
public class CreateGameRequest
{
    /// <summary>
    /// Game title
    /// </summary>
    [Required(ErrorMessage = "Введите заголовок")]
    [StringLength(GameFieldLimits.TitleMaxLength, MinimumLength = GameFieldLimits.TitleMinLength,
        ErrorMessage = "Заголовок от 3 до 200 символов")]
    public string Title { get; set; } = "";

    /// <summary>
    /// RPG system name (e.g. "D&amp;D 5e", "GURPS", "Fate")
    /// </summary>
    [StringLength(GameFieldLimits.SystemMaxLength, ErrorMessage = "Название системы не длиннее 100 символов")]
    public string? System { get; set; }

    /// <summary>
    /// Narrative setting (e.g. "Forgotten Realms", "Warhammer 40k", "Original")
    /// </summary>
    [StringLength(GameFieldLimits.SettingMaxLength, ErrorMessage = "Название сеттинга не длиннее 100 символов")]
    public string? Setting { get; set; }

    /// <summary>
    /// Game description/info (BB-code formatted)
    /// </summary>
    [Required(ErrorMessage = "Введите описание")]
    // The domain's rule rather than an invention of the form: a game is chosen
    // by this text, and the rule has been refusing short ones all along.
    [MinLength(GameFieldLimits.InfoMinLength, ErrorMessage = "Описание от 200 символов")]
    public string Info { get; set; } = "";

    /// <summary>
    /// Create game as a draft (not visible to others)
    /// </summary>
    public bool Draft { get; set; }

    /// <summary>
    /// Game tag short identifiers, the ones GET /games/tags serves
    /// </summary>
    public IEnumerable<int>? Tags { get; set; }

    /// <summary>
    /// Attribute schema identifier (for character attributes)
    /// </summary>
    public Guid? SchemaId { get; set; }

    /// <summary>
    /// Assistant user username (optional)
    /// </summary>
    public string? AssistantUsername { get; set; }

    /// <summary>
    /// Privacy settings for the game
    /// </summary>
    public CreateGamePrivacySettings? PrivacySettings { get; set; }
}

/// <summary>
/// Privacy settings for game creation
/// </summary>
public class CreateGamePrivacySettings
{
    /// <summary>
    /// Players can read private messages in posts
    /// </summary>
    public bool ViewPrivates { get; set; }

    /// <summary>
    /// Players can see dice roll results
    /// </summary>
    public bool ViewDice { get; set; } = true;

    /// <summary>
    /// Players can see post statistics
    /// </summary>
    public bool ViewPostStats { get; set; } = true;

    /// <summary>
    /// Access mode for game commentaries
    /// </summary>
    public CommentariesAccessMode CommentariesAccess { get; set; } = CommentariesAccessMode.Public;
}
