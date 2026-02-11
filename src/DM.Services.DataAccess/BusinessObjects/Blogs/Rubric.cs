using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.DataAccess.BusinessObjects.DataContracts;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.DataAccess.BusinessObjects.Blogs;

/// <summary>
/// DAL model for blog rubric (category)
/// </summary>
[Table("Rubrics")]
public class Rubric : ISoftDeletable
{
    /// <summary>
    /// Rubric identifier
    /// </summary>
    [Key]
    public Guid RubricId { get; set; }

    /// <summary>
    /// Parent blog identifier
    /// </summary>
    public Guid BlogId { get; set; }

    /// <summary>
    /// Rubric title
    /// </summary>
    [MaxLength(100)]
    public string Title { get; set; } = "";

    /// <summary>
    /// Sort order within blog
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Creation moment (UTC)
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedAtUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Parent blog
    /// </summary>
    [ForeignKey(nameof(BlogId))]
    public virtual Blog Blog { get; set; } = null!;

    /// <summary>
    /// User who deleted the rubric
    /// </summary>
    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }

    /// <summary>
    /// Publications in this rubric
    /// </summary>
    [InverseProperty(nameof(Publication.Rubric))]
    public virtual ICollection<Publication> Publications { get; set; } = [];

    #endregion
}
