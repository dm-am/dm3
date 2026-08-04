using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for an award type (timeless catalog). A specific contest series
/// is stored in ContestSeries, a grant in UserAward with FKs to both.
/// </summary>
[Table("AwardTypes")]
public class AwardType
{
    [Key]
    public Guid AwardTypeId { get; set; }

    [Required]
    [MaxLength(80)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(80)]
    public string IconName { get; set; } = string.Empty;

    public int? Tier { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}
