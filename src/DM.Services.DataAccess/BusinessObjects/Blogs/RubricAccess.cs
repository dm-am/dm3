using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.DataAccess.BusinessObjects.Blogs;

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
    /// Blog participant identifier
    /// </summary>
    public Guid BlogParticipantId { get; set; }

    /// <summary>
    /// Access policy for this participant on this rubric
    /// </summary>
    public RubricAccessPolicy Policy { get; set; }

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
    /// Blog participant
    /// </summary>
    [ForeignKey(nameof(BlogParticipantId))]
    public virtual BlogParticipant Participant { get; set; } = null!;

    #endregion
}
