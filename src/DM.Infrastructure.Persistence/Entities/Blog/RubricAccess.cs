using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;

namespace DM.Infrastructure.Persistence.Entities.Blog;

/// <summary>
/// DAL model for rubric access control
/// </summary>
[Table("RubricAccesses")]
public class RubricAccess
{
    /// <summary>
    /// Access entry identifier
    /// </summary>
    [Key]
    public Guid RubricAccessId { get; set; }

    /// <summary>
    /// Rubric identifier
    /// </summary>
    public Guid RubricId { get; set; }

    /// <summary>
    /// User identifier (reader with access to this rubric)
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// When access was granted
    /// </summary>
    public DateTimeOffset GrantedUtc { get; set; }

    #region Navigation Properties

    /// <summary>
    /// Rubric
    /// </summary>
    [ForeignKey(nameof(RubricId))]
    public virtual Rubric Rubric { get; set; } = null!;

    /// <summary>
    /// User with access
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    #endregion
}
