using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for comment
/// </summary>
[Table("Comments")]
public class Comment : ISoftDeletable, IHasEditHistory<CommentEdit>
{
    /// <summary>
    /// comment identifier
    /// </summary>
    [Key]
    public Guid CommentId { get; set; }

    /// <summary>
    /// Owner identifier: a polymorphic reference to a topic, game, blog or
    /// publication. No database foreign key stands behind it, and the column
    /// carries no discriminator - the owner type comes from the reading query.
    /// </summary>
    public Guid EntityId { get; set; }

    /// <summary>
    /// Author identifier
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// comment content
    /// </summary>
    public string Text { get; set; } = null!;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>
    /// Parent topic (for forum comments)
    /// Note: Configured in DmDbContext, not via ForeignKey attribute
    /// </summary>
    public virtual Topic? Topic { get; set; }

    /// <summary>
    /// Parent game (for game comments)
    /// Note: EntityId is a polymorphic reference - no DB FK exists
    /// </summary>
    [NotMapped]
    public virtual Game.Game? Game { get; set; }

    /// <summary>
    /// Parent blog (for blog discussion comments)
    /// Note: EntityId is a polymorphic reference - no DB FK exists
    /// </summary>
    [NotMapped]
    public virtual Blog.Blog? Blog { get; set; }

    /// <summary>
    /// Parent publication (for publication comments)
    /// Note: EntityId is a polymorphic reference - no DB FK exists
    /// </summary>
    [NotMapped]
    public virtual Publication? Publication { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    [ForeignKey(nameof(AuthorId))]
    public virtual User Author { get; set; } = null!;

    /// <summary>
    /// User who deleted the comment
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(CommentEdit.Comment))]
    public virtual ICollection<CommentEdit> Edits { get; set; } = [];

}
