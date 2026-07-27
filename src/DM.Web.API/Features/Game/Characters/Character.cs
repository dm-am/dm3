using System;
using DM.Domain.Core.Enums;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Characters;

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
    /// Character author (lightweight reference). Server-filled on read (absent
    /// for NPC characters); ignored on create/update input, hence nullable —
    /// a non-nullable reference would make model binding require it in POST
    /// and PATCH bodies.
    /// </summary>
    public UserRef? Author { get; set; }

    /// <summary>
    /// Character status
    /// </summary>
    public CharacterStatus? Status { get; set; }

    /// <summary>
    /// Character is dead (only meaningful for Retired status)
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player has voluntarily left the game (only meaningful for Retired status)
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player has been exiled from the game by master (only meaningful for Retired status)
    /// </summary>
    public bool IsPlayerExiled { get; set; }

    /// <summary>
    /// Total characters posts count
    /// </summary>
    public int TotalPostsCount { get; set; }

    /// <summary>
    /// Timestamp of the character's most recent post (null if it never posted).
    /// Column "Последний ход" on the game main-page roster.
    /// </summary>
    public DateTimeOffset? LastPostUtc { get; set; }

    /// <summary>
    /// Character's descriptor attribute value ("Класс" on the game main-page
    /// roster). Null when the game schema has no descriptor specification or the
    /// character has no value for it.
    /// </summary>
    public string? Descriptor { get; set; }

    /// <summary>
    /// The character author's rating ("Рейтинг" column). Null for NPC characters
    /// (no author) or when the author has disabled rating display.
    /// </summary>
    public Rating? AuthorRating { get; set; }

    /// <summary>
    /// Character name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Character avatar (3 variants). Symmetric with User.Picture.
    /// All URLs are null if the character has no uploaded avatar.
    /// </summary>
    public UserPicture Picture { get; set; } = new();

    /// <summary>
    /// Character is NPC (controlled by game master)
    /// </summary>
    public bool IsNpc { get; set; }
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
    /// Specification title. Server-filled on read; ignored on create/update
    /// input (only Id and Value reach the domain), hence nullable so model
    /// binding does not require it in request bodies.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Attribute value for non-BBCode specifications (plain string). Null for
    /// BBCode specifications, whose value is carried by <see cref="ValueBbText"/>.
    /// On update the raw value is always read from this field.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Server-rendered value for BBCode specifications. Non-null only when the
    /// backing specification is <see cref="AttributeSpecificationType.BbCode"/>;
    /// the raw stored BBCode is never emitted into an HTML sink.
    /// </summary>
    public InfoBbText? ValueBbText { get; set; }

    /// <summary>
    /// Attribute modifier
    /// </summary>
    public int? Modifier { get; set; }

    /// <summary>
    /// Flag of the required value missing
    /// </summary>
    public bool Inconsistent { get; set; }
}
