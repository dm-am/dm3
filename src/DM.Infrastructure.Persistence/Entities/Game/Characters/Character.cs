using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters;

/// <summary>
/// DAL model for character
/// </summary>
[Table("Characters")]
public class Character : ISoftDeletable, IHasEditHistory<CharacterEdit>
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
    /// Author identifier (null for NPC)
    /// </summary>
    public Guid? AuthorId { get; set; }

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
    /// Name
    /// </summary>
    public string Name { get; set; } = null!;

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
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Game
    /// </summary>
    [ForeignKey(nameof(GameId))]
    public virtual Game Game { get; set; } = null!;

    /// <summary>
    /// Author (null for NPC)
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User? Author { get; set; }

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
