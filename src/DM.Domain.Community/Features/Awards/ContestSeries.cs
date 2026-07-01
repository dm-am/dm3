using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Серия конкурса. Каждый конкурс — отдельная запись со сквозным
/// глобальным <see cref="Number"/> в рамках типа. Награды (UserAward)
/// ссылаются на серию через FK; типы наград (<see cref="AwardType"/>)
/// timeless и не дублируются для каждого года.
/// </summary>
public class ContestSeries
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }

    /// <summary>Тип конкурса (литературный, арт и т.д.).</summary>
    public ContestType ContestType { get; set; }

    /// <summary>
    /// Сквозной номер конкурса в рамках типа (23-й литературный, 1-й арт).
    /// UNIQUE(ContestType, Number).
    /// </summary>
    public int Number { get; set; }

    /// <summary>Год проведения (отображается в year-бейдже на тайле).</summary>
    public int Year { get; set; }

    /// <summary>Ссылка на форумный топик с итогами конкурса (опционально).</summary>
    public string? TopicUrl { get; set; }

    /// <summary>Активна = доступна для выдачи наград (в dropdown).</summary>
    public bool IsActive { get; set; }
}

/// <summary>Запрос на создание новой серии.</summary>
public class CreateContestSeries
{
    /// <summary>Тип конкурса.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Сквозной номер в рамках типа.</summary>
    public int Number { get; set; }
    /// <summary>Год.</summary>
    public int Year { get; set; }
    /// <summary>Ссылка на топик с итогами (опц).</summary>
    public string? TopicUrl { get; set; }
}

/// <summary>Запрос на частичное обновление серии.</summary>
public class UpdateContestSeries
{
    /// <summary>Идентификатор обновляемой записи.</summary>
    public Guid Id { get; set; }
    /// <summary>Новый ContestType (null = не менять).</summary>
    public ContestType? ContestType { get; set; }
    /// <summary>Новый Number (null = не менять).</summary>
    public int? Number { get; set; }
    /// <summary>Новый Year (null = не менять).</summary>
    public int? Year { get; set; }
    /// <summary>Новый TopicUrl (null = не менять; пустая строка = очистить).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>Новый IsActive (null = не менять).</summary>
    public bool? IsActive { get; set; }
}
