#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Achievements.UserAchievement
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>DAL for the fact that a user earned an achievement.</summary>
[Table("UserAchievements")]
public class UserAchievement : IRemovable
{
    [Key]
    public Guid UserAchievementId { get; set; }

    public Guid UserId { get; set; }

    public Guid AchievementTypeId { get; set; }

    public DateTimeOffset EarnedUtc { get; set; }

    /// <inheritdoc />
    public bool IsRemoved { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(AchievementTypeId))]
    public virtual AchievementType AchievementType { get; set; } = null!;
}
