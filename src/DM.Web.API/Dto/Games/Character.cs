using System;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Games;

/// <summary>
/// DTO model for game character (lightweight, for lists)
/// </summary>
public class Character
{
    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Character author
    /// </summary>
    public User Author { get; set; } = null!;

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus? Status { get; set; }

    /// <summary>
    /// Total characters posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character race
    /// </summary>
    public string Race { get; set; } = null!;

    /// <summary>
    /// Character class
    /// </summary>
    public string Class { get; set; } = null!;

    /// <summary>
    /// Character picture URL
    /// </summary>
    public string PictureUrl { get; set; } = null!;
}

/// <summary>
/// DTO model for character privacy settings
/// </summary>
public class CharacterPrivacySettings
{
    /// <summary>
    /// Character is non-player-character
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// Character may be edited by master or assistant
    /// </summary>
    public bool EditByMaster { get; set; }

    /// <summary>
    /// Character's posts may be edited by master or assistant
    /// </summary>
    public bool EditPostByMaster { get; set; }
}

/// <summary>
/// DTO model for character attribute value
/// </summary>
public class CharacterAttribute
{
    /// <summary>
    /// Specification identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Specification title
    /// </summary>
    public string Title { get; set; } = null!;

    /// <summary>
    /// Attribute value
    /// </summary>
    public string Value { get; set; } = null!;

    /// <summary>
    /// Attribute modifier
    /// </summary>
    public int? Modifier { get; set; }

    /// <summary>
    /// Flag of the required value missing
    /// </summary>
    public bool Inconsistent { get; set; }
}
