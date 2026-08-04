using System;
using System.Collections.Generic;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Features.Games;

/// <summary>
/// DTO model for new game
/// </summary>
public class CreateGame
{
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
    public bool HideDiceResult { get; set; }

    /// <summary>
    /// Everyone can read each others private messages
    /// </summary>
    public bool ShowPrivateMessages { get; set; }

    /// <summary>
    /// Hide posts count and last post date for all users (except Master/Assistant)
    /// </summary>
    public bool HidePostStats { get; set; }

    /// <summary>
    /// comments access mode
    /// </summary>
    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// Attribute schema identifier
    /// </summary>
    public Guid? AttributeSchemaId { get; set; }

    /// <summary>
    /// Game tag short identifiers: the public alias the tag list and the game
    /// filters speak, translated to the tags' own identifiers on the way in
    /// </summary>
    public IEnumerable<int> Tags { get; set; } = [];

    /// <summary>
    /// Create game as a draft
    /// </summary>
    public bool Draft { get; set; }

    /// <summary>
    /// Visibility of draft content (when Draft = true)
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Copy personal blacklist to game blacklist on creation
    /// </summary>
    public bool CopyBlacklist { get; set; }
}
