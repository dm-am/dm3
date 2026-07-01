using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Тир достижения в цепочке. Хранит только то, что уникально для тира:
/// порог, ранг, имя. Все, что одинаково для всей цепочки (иконка,
/// описание, метрика, sort-order, активность), — на <see cref="AchievementCategory"/>.
/// </summary>
public class AchievementType
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }

    /// <summary>Стабильный код тира («POSTS_100»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Отображаемое название тира («Автор»).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Порог метрики для разблокировки.</summary>
    public int Threshold { get; set; }

    /// <summary>
    /// Визуальный тир (1=bronze, 2=silver, 3=gold, 4=platinum).
    /// Не влияет на логику, только на UI.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Категория-родитель (хранит метрику, иконку, описание).</summary>
    public AchievementCategory Category { get; set; } = null!;
}

/// <summary>Запрос на создание нового тира в каталоге достижений.</summary>
public class CreateAchievementType
{
    /// <summary>Стабильный код.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название тира.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Порог разблокировки.</summary>
    public int Threshold { get; set; }
    /// <summary>Тир (визуальный стиль 1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Идентификатор категории-родителя.</summary>
    public Guid AchievementCategoryId { get; set; }
}

/// <summary>Запрос на частичное обновление тира.</summary>
public class UpdateAchievementType
{
    /// <summary>Идентификатор обновляемой записи.</summary>
    public Guid Id { get; set; }
    /// <summary>Новое название (null = не менять).</summary>
    public string? Title { get; set; }
    /// <summary>Новый порог (null = не менять).</summary>
    public int? Threshold { get; set; }
    /// <summary>Новый тир (null = не менять).</summary>
    public int? Tier { get; set; }
}
