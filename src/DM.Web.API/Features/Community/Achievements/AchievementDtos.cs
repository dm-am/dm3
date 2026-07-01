using System;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Community.Achievements;

/// <summary>API DTO: категория достижений (цепочка тиров одной метрики).</summary>
public class AchievementCategory
{
    /// <summary>Идентификатор.</summary>
    public Guid Id { get; set; }
    /// <summary>Стабильный код ("game_posts_authored").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название цепочки ("Игровые посты").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Описание метрики (что именно считается).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Имя иконки из game-icons спрайта.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Метрика.</summary>
    public AchievementMetric Metric { get; set; }
    /// <summary>Порядок цепочек в UI.</summary>
    public int SortOrder { get; set; }
    /// <summary>Активна ли категория.</summary>
    public bool IsActive { get; set; }
}

/// <summary>API DTO: тир достижения. Категория-родитель включена в ответ.</summary>
public class AchievementType
{
    /// <summary>Идентификатор тира.</summary>
    public Guid Id { get; set; }
    /// <summary>Стабильный код ("POSTS_100").</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название тира ("Автор").</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Порог разблокировки.</summary>
    public int Threshold { get; set; }
    /// <summary>Визуальный тир (1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Категория-родитель (SSOT для иконки, метрики, описания цепочки).</summary>
    public AchievementCategory Category { get; set; } = null!;
}

/// <summary>API DTO: факт получения достижения пользователем.</summary>
public class UserAchievement
{
    /// <summary>Идентификатор записи о получении.</summary>
    public Guid Id { get; set; }
    /// <summary>Тип достижения.</summary>
    public AchievementType Type { get; set; } = null!;
    /// <summary>Момент пересечения порога (UTC).</summary>
    public DateTimeOffset EarnedUtc { get; set; }
}

/// <summary>Запрос на обновление категории. Создание не предусмотрено: каталог статичен.</summary>
public class UpdateAchievementCategoryRequest
{
    /// <summary>Новое название.</summary>
    public string? Title { get; set; }
    /// <summary>Новое описание.</summary>
    public string? Description { get; set; }
    /// <summary>Новая иконка.</summary>
    public string? IconName { get; set; }
    /// <summary>Новый SortOrder.</summary>
    public int? SortOrder { get; set; }
    /// <summary>Новый IsActive.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Запрос на создание нового тира.</summary>
public class CreateAchievementTypeRequest
{
    /// <summary>Стабильный код (уникальный).</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название тира.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Порог разблокировки.</summary>
    public int Threshold { get; set; }
    /// <summary>Визуальный тир (1-4).</summary>
    public int? Tier { get; set; }
    /// <summary>Идентификатор категории-родителя.</summary>
    public Guid AchievementCategoryId { get; set; }
}

/// <summary>Запрос на частичное обновление тира.</summary>
public class UpdateAchievementTypeRequest
{
    /// <summary>Новое название.</summary>
    public string? Title { get; set; }
    /// <summary>Новый порог.</summary>
    public int? Threshold { get; set; }
    /// <summary>Новый тир.</summary>
    public int? Tier { get; set; }
}
