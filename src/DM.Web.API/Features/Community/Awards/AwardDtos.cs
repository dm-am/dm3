using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Community.Awards;

/// <summary>API DTO: запись каталога типов наград (timeless).</summary>
public class AwardType
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }
    /// <summary>Стабильный код ("contest_first", "popular_vote").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Отображаемое название ("Литконкурс", "Народное признание").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Описание (за что выдается).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Имя иконки из game-icons спрайта.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Визуальный тир (1=gold, 2=silver, 3=bronze).</summary>
    public int? Tier { get; set; }
    /// <summary>Порядок отображения внутри серии конкурса.</summary>
    public int SortOrder { get; set; }
    /// <summary>Активный ли тип.</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: серия конкурса.</summary>
public class ContestSeries
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }
    /// <summary>Тип конкурса.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Сквозной номер конкурса в рамках типа (23-й литературный, 1-й арт).</summary>
    public int Number { get; set; }
    /// <summary>Год проведения (отображается в year-бейдже на тайле).</summary>
    public int Year { get; set; }
    /// <summary>Ссылка на форумный топик с итогами (опционально).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>Активна ли серия (видна в dropdown).</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: награда, выданная пользователю.</summary>
public class UserAward
{
    /// <summary>Идентификатор записи о выдаче.</summary>
    public Guid Id { get; set; }
    /// <summary>Тип награды.</summary>
    public AwardType Type { get; set; } = null!;
    /// <summary>Серия конкурса (опционально для будущих внеконкурсных наград).</summary>
    public ContestSeries? ContestSeries { get; set; }
    /// <summary>
    /// Ссылка на форумный топик с работой, за которую получена награда
    /// (опционально — best_critic / guesser не привязаны к работе).
    /// </summary>
    public string? WorkUrl { get; set; }
    /// <summary>Момент выдачи (UTC).</summary>
    public DateTimeOffset AwardedUtc { get; set; }
}

/// <summary>Запрос на создание записи каталога типов наград.</summary>
public class CreateAwardTypeRequest
{
    /// <summary>Стабильный код. Уникальный.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Описание (за что выдается).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Имя иконки.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Визуальный тир.</summary>
    public int? Tier { get; set; }
    /// <summary>Порядок отображения.</summary>
    public int SortOrder { get; set; }
}

/// <summary>Запрос на частичное обновление типа награды. null-поля = не трогать.</summary>
public class UpdateAwardTypeRequest
{
    /// <summary>Новое название.</summary>
    public string? Title { get; set; }
    /// <summary>Новое описание.</summary>
    public string? Description { get; set; }
    /// <summary>Новая иконка.</summary>
    public string? IconName { get; set; }
    /// <summary>Новый тир.</summary>
    public int? Tier { get; set; }
    /// <summary>Новый порядок.</summary>
    public int? SortOrder { get; set; }
    /// <summary>Новый IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Запрос на создание серии конкурса.</summary>
public class CreateContestSeriesRequest
{
    /// <summary>Тип конкурса.</summary>
    public ContestType ContestType { get; set; }
    /// <summary>Сквозной номер в рамках типа.</summary>
    public int Number { get; set; }
    /// <summary>Год.</summary>
    public int Year { get; set; }
    /// <summary>Ссылка на топик (опц).</summary>
    public string? TopicUrl { get; set; }
}

/// <summary>Запрос на частичное обновление серии. null-поля = не трогать.</summary>
public class UpdateContestSeriesRequest
{
    /// <summary>Новый ContestType.</summary>
    public ContestType? ContestType { get; set; }
    /// <summary>Новый Number.</summary>
    public int? Number { get; set; }
    /// <summary>Новый Year.</summary>
    public int? Year { get; set; }
    /// <summary>Новый TopicUrl (null = не менять, "" = очистить).</summary>
    public string? TopicUrl { get; set; }
    /// <summary>Новый IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Запрос на выдачу награды пользователю.</summary>
public class GrantUserAwardRequest
{
    /// <summary>Идентификатор типа награды из каталога.</summary>
    public Guid AwardTypeId { get; set; }
    /// <summary>Идентификатор серии конкурса (опционально).</summary>
    public Guid? ContestSeriesId { get; set; }
    /// <summary>Ссылка на форумный топик с работой (опционально).</summary>
    public string? WorkUrl { get; set; }
}
