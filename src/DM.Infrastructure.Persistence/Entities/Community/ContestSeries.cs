#pragma warning disable CS1591 // DAL entity — fields are self-documenting; see Domain.Community.Features.Awards.ContestSeries
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DM.Domain.Core.Enums;

namespace DM.Infrastructure.Persistence.Entities.Community;

/// <summary>
/// DAL для серии конкурса. Каждый конкурс — отдельная запись
/// с глобальным сквозным номером в рамках типа (Literary 1..N, Art 1..M).
/// Награды (UserAward) ссылаются на серию, а не на per-year AwardType,
/// что позволяет каталог типов держать timeless (6 строк).
/// </summary>
[Table("ContestSeries")]
public class ContestSeries
{
    [Key]
    public Guid ContestSeriesId { get; set; }

    /// <summary>
    /// Тип конкурса. У каждого типа своя сквозная нумерация
    /// (Literary 1..N, Art 1..M, …). UNIQUE(ContestType, Number).
    /// </summary>
    public ContestType ContestType { get; set; }

    /// <summary>
    /// Сквозной номер конкурса в рамках типа (23-й литературный, 1-й арт).
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Год проведения конкурса. Чисто отображаемое поле — на тайле награды
    /// показывается в year-бейдже. Уникальности по году не требуется.
    /// </summary>
    public int Year { get; set; }

    /// <summary>
    /// Ссылка на форумный топик с итогами конкурса. Опциональна — серия
    /// может существовать до публикации итогов или вообще без публичного топика.
    /// </summary>
    [MaxLength(500)]
    public string? TopicUrl { get; set; }

    public bool IsActive { get; set; }

    [InverseProperty(nameof(UserAward.ContestSeries))]
    public virtual ICollection<UserAward> Awards { get; set; } = [];
}
