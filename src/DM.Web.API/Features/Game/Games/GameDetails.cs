using System;
using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Game.AttributeSchemas;

namespace DM.Web.API.Features.Game.Games;

/// <summary>
/// API DTO model for game details (extended game info)
/// </summary>
public class GameDetails : Game
{
    /// <summary>
    /// Game public information (BBCode)
    /// </summary>
    public InfoBbText Info { get; set; } = null!;

    /// <summary>
    /// Game privacy settings
    /// </summary>
    public GamePrivacySettings PrivacySettings { get; set; } = null!;

    /// <summary>
    /// Attribute schema details
    /// </summary>
    public AttributeSchema? Schema { get; set; }

    /// <summary>
    /// Game assistants (lightweight references for detail page)
    /// </summary>
    public IEnumerable<UserRef> FullAssistants { get; set; } = [];

    /// <summary>
    /// Game subscribers (lightweight references)
    /// </summary>
    public IEnumerable<UserRef> Subscribers { get; set; } = [];

    /// <summary>
    /// Game readers ("Читатели") - the users subscribed to the game. A reader is
    /// exactly a game subscriber in the domain (GameRole.Reader is subscription
    /// based), so this is the same source as <see cref="Subscribers"/>, surfaced
    /// under the roster's expected name.
    /// </summary>
    public IEnumerable<UserRef> Readers { get; set; } = [];

    /// <summary>
    /// Total number of posts across all rooms ("Постов всего").
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Number of posts authored by the game master ("Постов мастера").
    /// </summary>
    public int MasterPostsCount { get; set; }

    /// <summary>
    /// Timestamp of the game master's most recent post ("Последний пост мастера").
    /// Null if the master has not posted.
    /// </summary>
    public DateTimeOffset? LastMasterPostUtc { get; set; }

    /// <summary>
    /// Whether the game supports dice rolls ("Поддержка кубика") - true when any
    /// room of the game has dice rolling enabled.
    /// </summary>
    public bool DiceSupported { get; set; }

    /// <summary>
    /// Game characters (short info)
    /// </summary>
    public IEnumerable<CharacterShortInfo> Characters { get; set; } = [];
}

/// <summary>
/// Short character info for game details
/// </summary>
public class CharacterShortInfo
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public System.Guid Id { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character owner (lightweight reference). Absent for an NPC: the game
    /// master runs it and no player owns it. Same shape as
    /// <see cref="Characters.Character.Author"/>, which has always been
    /// nullable for the same reason.
    /// </summary>
    public UserRef? Author { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public DM.Domain.Core.Enums.CharacterStatus Status { get; set; }
}
