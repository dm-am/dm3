using System;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Features.Achievements;

/// <summary>
/// Категория достижений = цепочка тиров одной метрики. SSOT для иконки,
/// описания и порядка. Одна метрика = одна категория (UNIQUE на уровне БД).
/// </summary>
public class AchievementCategory
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }

    /// <summary>Стабильный код («game_posts_authored»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Отображаемое название цепочки («Игровые посты»).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Описание метрики — что именно считается, что включается, что нет.
    /// Показывается в шапке popover-а на достижении.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Имя иконки из game-icons спрайта (одна на всю цепочку).</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>
    /// Метрика. По ней evaluator считает прогресс. Маппится на поле
    /// <c>GeneralUser</c> через <c>AchievementMetricResolver.GetValue</c>.
    /// </summary>
    public AchievementMetric Metric { get; set; }

    /// <summary>Порядок цепочек в UI.</summary>
    public int SortOrder { get; set; }

    /// <summary>Активна = тиры доступны для начисления и видны в UI.</summary>
    public bool IsActive { get; set; }
}

/// <summary>Запрос на обновление категории. Создание из админ-UI не предполагается:
/// каталог из 13 категорий статичен; правки — точечные (description, icon).</summary>
public class UpdateAchievementCategory
{
    /// <summary>Идентификатор обновляемой записи.</summary>
    public Guid Id { get; set; }
    /// <summary>Новое название (null = не менять).</summary>
    public string? Title { get; set; }
    /// <summary>Новое описание (null = не менять).</summary>
    public string? Description { get; set; }
    /// <summary>Новая иконка (null = не менять).</summary>
    public string? IconName { get; set; }
    /// <summary>Новый SortOrder (null = не менять).</summary>
    public int? SortOrder { get; set; }
    /// <summary>Новый IsActive (null = не менять).</summary>
    public bool? IsActive { get; set; }
}
