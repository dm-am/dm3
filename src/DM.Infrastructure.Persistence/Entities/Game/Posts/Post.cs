using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Game.Posts;

/// <summary>
/// DAL model for game post
/// </summary>
[Table("Posts")]
public class Post : ISoftDeletable
{
    [Key]
    public Guid PostId { get; set; }

    public Guid RoomId { get; set; }

    /// <summary>
    /// Character identifier
    /// </summary>
    public Guid? CharacterId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Game text (in-character content)
    /// </summary>
    public string GameText { get; set; } = null!;

    /// <summary>
    /// Metagame text (OOC commentary)
    /// </summary>
    public string? MetagameText { get; set; }


    /// <summary>
    /// Snapshot of owner-user ids allowed to see each [private] block in
    /// GameText, keyed by the raw tag attribute string the author wrote
    /// (character names). Resolved at save time and immutable thereafter;
    /// preserves addressee visibility even if a character later leaves the
    /// game. Stored as JSONB (PostgreSQL) — a dictionary &lt;string, Guid[]&gt;.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string PrivateAddresseeSnapshotJson { get; set; } = "{}";

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    [ForeignKey(nameof(RoomId))]
    public virtual Room Room { get; set; } = null!;

    /// <summary>
    /// Character
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character? Character { get; set; }

    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the post
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(PostEdit.Post))]
    public virtual ICollection<PostEdit> Edits { get; set; } = [];


    [InverseProperty(nameof(PostReview.Post))]
    public virtual ICollection<PostReview> Reviews { get; set; } = [];
}
