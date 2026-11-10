using System.Collections.Generic;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Features.Games;

public class CreateGame
{
    public string Title { get; set; } = null!;

    public string SystemName { get; set; } = null!;

    /// <summary>
    /// Narrative setting (e.g. Mass Effect, Warhammer, Our world)
    /// </summary>
    public string NarrativeSetting { get; set; } = null!;

    public string Info { get; set; } = null!;

    public string AssistantUsername { get; set; } = null!;

    /// <summary>
    /// Only the leads and the post author see a roll result
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

    public CommentsAccessMode CommentsAccessMode { get; set; }

    /// <summary>
    /// The sheet the game starts with; an empty set is a game whose characters carry no attributes
    /// </summary>
    public IEnumerable<CreateAttributeSpecification> AttributeSpecifications { get; set; } = [];

    /// <summary>
    /// Short identifiers of the tags, the alias the tag list and the filters speak
    /// </summary>
    public IEnumerable<int> Tags { get; set; } = [];

    public bool Draft { get; set; }

    public DraftVisibility DraftVisibility { get; set; }

    /// <summary>
    /// Copy the creator's personal blacklist into the game's; null leaves it to AutoPopulateContentBlacklist.
    /// </summary>
    /// <remarks>
    /// Off unless the creator turns it on: copying shows his personal list to whoever manages the game's.
    /// </remarks>
    public bool? CopyBlacklist { get; set; }
}
