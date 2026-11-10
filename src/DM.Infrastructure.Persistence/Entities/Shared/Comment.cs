using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Contracts;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Shared;

/// <summary>
/// DAL model for comment
/// </summary>
[Table("Comments")]
public class Comment : ISoftDeletable
{
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
    /// Note: Configured in ForumModel, not via ForeignKey attribute
    /// </summary>
    public virtual Topic? Topic { get; set; }


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
