using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Community.Features.Catalogs;

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
    [MaxLength(CatalogFieldLimits.CodeMaxLength)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.TitleMaxLength)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.DescriptionMaxLength)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(CatalogFieldLimits.IconNameMaxLength)]
    public string IconName { get; set; } = string.Empty;

    public int? Tier { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}
