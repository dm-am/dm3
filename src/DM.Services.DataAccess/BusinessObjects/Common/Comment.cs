using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.Administration;
using DM.Services.DataAccess.BusinessObjects.Blogs;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Boards;
using DM.Services.DataAccess.BusinessObjects.Games;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Common;

/// <summary>
/// DAL model for commentary
/// </summary>
[Table("Comments")]
public class Comment : ISoftDeletable, IEditable, IHasEditHistory<CommentEdit>
{
    /// <summary>
    /// Commentary identifier
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
    public Guid UserId { get; set; }

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
    /// Commentary content
    /// </summary>
    public string Text { get; set; } = null!;

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

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
    public virtual Game? Game { get; set; }

    /// <summary>
    /// Parent blog (for blog discussion comments)
    /// Note: EntityId is a polymorphic reference - no DB FK exists
    /// </summary>
    [NotMapped]
    public virtual Blog? Blog { get; set; }

    /// <summary>
    /// Parent publication (for publication comments)
    /// Note: EntityId is a polymorphic reference - no DB FK exists
    /// </summary>
    [NotMapped]
    public virtual Publication? Publication { get; set; }

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