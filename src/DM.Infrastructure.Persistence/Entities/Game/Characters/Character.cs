using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Characters;

/// <summary>
/// DAL model for character
/// </summary>
[Table("Characters")]
public class Character : ISoftDeletable
{
    [Key]
    public Guid CharacterId { get; set; }

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

    [MaxLength(CharacterPolicy.NameMaxLength)]
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

    [InverseProperty(nameof(CharacterAttribute.Character))]
    public virtual ICollection<CharacterAttribute> Attributes { get; set; } = [];

    [InverseProperty(nameof(Post.Character))]
    public virtual ICollection<Post> Posts { get; set; } = [];
}
