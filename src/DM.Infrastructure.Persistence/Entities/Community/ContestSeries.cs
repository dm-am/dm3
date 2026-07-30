#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Awards.ContestSeries
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for a contest series. Each contest is a separate record
/// with a global sequential number within its type (Literary 1..N, Art 1..M).
/// Awards (UserAward) reference the series rather than a per-year AwardType,
/// which keeps the type catalog timeless.
/// </summary>
[Table("ContestSeries")]
public class ContestSeries
{
    [Key]
    public Guid ContestSeriesId { get; set; }

    /// <summary>
    /// Contest type. Each type has its own sequential numbering
    /// (Literary 1..N, Art 1..M, …). UNIQUE(ContestType, Number).
    /// </summary>
    public ContestType ContestType { get; set; }

    /// <summary>
    /// Sequential contest number within the type (23rd literary, 1st art).
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Contest year. Purely a display field — shown in the year badge
    /// on the award tile. No uniqueness by year is required.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Link to the forum topic with contest results. Optional — a series
    /// may exist before the results are published or without a public topic at all.
    /// </summary>
    [MaxLength(500)]
    public string? TopicUrl { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty(nameof(UserAward.ContestSeries))]
    public virtual ICollection<UserAward> Awards { get; set; } = [];
}
