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
public class Comment : ISoftDeletable, IEditable, IHasEditHistory<CommentEditHistory>
{
    /// <summary>
    /// comment identifier
    /// </summary>
    [Key]
    public Guid CommentId { get; set; }

    /// <summary>
    /// Forum topic identifier
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
    /// Last modification moment (UTC)
    /// </summary>
    public DateTimeOffset? ModifiedUtc { get; set; }

    /// <summary>
    /// Last editor user identifier
    /// </summary>
    public Guid? ModifiedByUserId { get; set; }

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
    /// Last editor
    /// </summary>
    [ForeignKey(nameof(ModifiedByUserId))]
    public virtual User? ModifiedBy { get; set; }

    /// <summary>
    /// User who deleted the comment
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Edit history
    /// </summary>
    [InverseProperty(nameof(CommentEditHistory.Comment))]
    public virtual ICollection<CommentEditHistory> Edits { get; set; } = [];

}
