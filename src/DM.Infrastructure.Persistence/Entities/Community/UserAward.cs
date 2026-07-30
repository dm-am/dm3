using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL for an award grant record. AwardedByUserId is stored
/// for audit (which admin/senior mod granted it) but is not returned
/// in the public API. ContestSeriesId is nullable, for future out-of-contest
/// awards; in the seeder all awards are tied to series.
/// </summary>
[Table("UserAwards")]
public class UserAward : ISoftDeletable
{
    [Key]
    public Guid UserAwardId { get; set; }

    public Guid UserId { get; set; }

    public Guid AwardTypeId { get; set; }

    public Guid? ContestSeriesId { get; set; }

    /// <summary>
    /// Link to the forum topic with the work itself (story / art /
    /// review) the award was granted for. Per-grant, because
    /// one award type can be granted to different people for different works.
    /// Optional (best_critic / guesser are not tied to a specific
    /// author's work).
    /// </summary>
    [MaxLength(500)]
    public string? WorkUrl { get; set; }

    public DateTimeOffset AwardedUtc { get; set; }

    public Guid AwardedByUserId { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    /// <inheritdoc />
    public Guid? DeletedByUserId { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? DeletedUtc { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(AwardTypeId))]
    public virtual AwardType AwardType { get; set; } = null!;

    [ForeignKey(nameof(ContestSeriesId))]
    public virtual ContestSeries? ContestSeries { get; set; }

    [ForeignKey(nameof(AwardedByUserId))]
    public virtual User AwardedBy { get; set; } = null!;

    [ForeignKey(nameof(DeletedByUserId))]
    public virtual User? DeletedBy { get; set; }
}
