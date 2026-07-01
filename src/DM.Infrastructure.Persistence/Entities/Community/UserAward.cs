#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Awards.UserAward
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Contracts;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL для записи о выдаче награды пользователю. AwardedByUserId хранится
/// для audit (кто из админов/сениор-модов выдал), но в публичном API
/// не отдается. ContestSeriesId — nullable, на случай будущих внеконкурсных
/// наград; в сидере все награды привязаны к сериям.
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
    /// Ссылка на форумный топик с самой работой (рассказом / артом /
    /// рецензией), за которую выдана награда. Per-grant, потому что
    /// один тип награды можно выдать разным людям за разные работы.
    /// Опциональна (best_critic / guesser — не привязаны к конкретной
    /// работе автора).
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
