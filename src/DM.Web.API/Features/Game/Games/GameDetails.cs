using System.Collections.Generic;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Features.Community.Users;
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
    /// Game private notes content (for GM)
    /// </summary>
    public string? Notepad { get; set; }

    /// <summary>
    /// Game privacy settings
    /// </summary>
    public GamePrivacySettings PrivacySettings { get; set; } = null!;

    /// <summary>
    /// Attribute schema details
    /// </summary>
    public AttributeSchema? Schema { get; set; }

    /// <summary>
    /// Game readers
    /// </summary>
    public IEnumerable<User> Readers { get; set; } = [];

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
    /// Character owner
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Character status
    /// </summary>
    public DM.Domain.Core.Enums.CharacterStatus Status { get; set; }
}
