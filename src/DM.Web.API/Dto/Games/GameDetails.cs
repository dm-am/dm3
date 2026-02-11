using System.Collections.Generic;
using DM.Web.API.BbRendering;
using DM.Web.API.Dto.Games.Attributes;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Games;

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
    public AttributeSchema Schema { get; set; } = null!;

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
    public DM.Services.Core.Dto.Enums.CharacterStatus Status { get; set; }
}
