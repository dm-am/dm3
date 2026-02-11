using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess.BusinessObjects.Common;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Games.Characters.Attributes;
using DM.Services.DataAccess.BusinessObjects.Games.Links;
using DM.Services.DataAccess.BusinessObjects.Games.Posts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Games.Characters;

/// <summary>
/// DAL model for character
/// </summary>
[Table("Characters")]
public class Character : ISoftDeletable, IEditable, IHasEditHistory<CharacterEdit>
{
    /// <summary>
    /// Character identifier
    /// </summary>
    [Key]
    public Guid CharacterId { get; set; }

    /// <summary>
    /// Game identifier
    /// </summary>
    public Guid GameId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Status
    /// </summary>
    public CharacterStatus Status { get; set; }

    /// <summary>
    /// Character died in game (only when Status = Retired)
    /// </summary>
    public bool IsDead { get; set; }

    /// <summary>
    /// Player left the game voluntarily (only when Status = Retired)
    /// </summary>
    public bool IsPlayerLeft { get; set; }

    /// <summary>
    /// Player was exiled from the game by GM (only when Status = Retired)
    /// </summary>
    public bool IsPlayerExiled { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

    /// <summary>
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Race (e.g. elf, asari, human)
    /// </summary>
    public string? Race { get; set; }

    /// <summary>
    /// Class (e.g. wizard, sniper)
    /// </summary>
    public string? Class { get; set; }

    /// <summary>
    /// Old-school-D&amp;D stuff
    /// </summary>
    public Alignment? Alignment { get; set; }

    /// <summary>
    /// Appearance
    /// </summary>
    public string? Appearance { get; set; }

    /// <summary>
    /// Temper
    /// </summary>
    public string? Temper { get; set; }

    /// <summary>
    /// Life story
    /// </summary>
    public string? Story { get; set; }

    /// <summary>
    /// Skills
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// Inventory
    /// </summary>
    public string? Inventory { get; set; }

    /// <summary>
    /// NPC flag
    /// </summary>
    public bool IsNpc { get; set; }

    /// <summary>
    /// GM access policy
    /// </summary>
    public CharacterAccessPolicy AccessPolicy { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>
    /// Game
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the character
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(CharacterEdit.Character))]
    public virtual ICollection<CharacterEdit> Edits { get; set; } = [];

    /// <summary>
    /// Portrait
    /// </summary>
    [InverseProperty(nameof(Upload.Character))]
    public virtual ICollection<Upload> Pictures { get; set; } = [];

    /// <summary>
    /// Attribute values
    /// </summary>
    [InverseProperty(nameof(CharacterAttribute.Character))]
    public virtual ICollection<CharacterAttribute> Attributes { get; set; } = [];

    /// <summary>
    /// Room access
    /// </summary>
    [InverseProperty(nameof(RoomAccess.Character))]
    public virtual ICollection<RoomAccess> RoomLinks { get; set; } = [];

    /// <summary>
    /// Posts
    /// </summary>
    [InverseProperty(nameof(Post.Character))]
    public virtual ICollection<Post> Posts { get; set; } = [];
}