using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Account;
using GameEntities = DM.Infrastructure.Persistence.Entities.Game;

namespace DM.Infrastructure.Persistence.Entities.Game.Posts;

/// <summary>
/// DAL model for game post
/// </summary>
[Table("Posts")]
public class Post : ISoftDeletable, IHasEditHistory<PostEdit>
{
    /// <summary>
    /// Post identifier
    /// </summary>
    [Key]
    public Guid PostId { get; set; }

    /// <summary>
    /// Room identifier
    /// </summary>
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

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Room
    /// </summary>
    [ForeignKey(nameof(RoomId))]
    public virtual Room Room { get; set; } = null!;

    /// <summary>
    /// Character
    /// </summary>
    [ForeignKey(nameof(CharacterId))]
    public virtual Character? Character { get; set; }

    /// <summary>
    /// Author
    /// </summary>
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

    // NOTE: Attachments navigation removed - Upload.EntityId is polymorphic without FK constraints

    /// <summary>
    /// Reviews for this post
    /// </summary>
    [InverseProperty(nameof(GameEntities.PostReview.Post))]
    public virtual ICollection<GameEntities.PostReview> Reviews { get; set; } = [];
}
