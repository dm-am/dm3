using System;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Каталог типов наград — timeless (6 строк). Конкретная серия конкурса
/// хранится в <see cref="ContestSeries"/>, выдача — в <see cref="UserAward"/>
/// с FK на оба. Иконка из game-icons спрайта (валидируется при создании).
/// </summary>
public class AwardType
{
    /// <summary>Идентификатор записи каталога.</summary>
    public Guid Id { get; set; }

    /// <summary>Стабильный код («contest_first», «popular_vote», «guesser»).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Отображаемое название («Литконкурс», «Народное признание»).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Описание (за что выдается).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Имя иконки из game-icons спрайта.</summary>
    public string IconName { get; set; } = string.Empty;

    /// <summary>
    /// Тир для визуального стиля (1=gold, 2=silver, 3=bronze).
    /// Для мест в литконкурсе: 1/2/3 = 1/2/3 место. Для спец-наград: 1.
    /// </summary>
    public int? Tier { get; set; }

    /// <summary>Порядок отображения внутри серии конкурса.</summary>
    public int SortOrder { get; set; }

    /// <summary>Активный = доступен для выдачи. Soft-delete через флаг.</summary>
    public bool IsActive { get; set; }
}

/// <summary>Запрос на создание новой записи в каталоге наград.</summary>
public class CreateAwardType
{
    /// <summary>Стабильный код.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>Название.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Описание.</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Имя иконки.</summary>
    public string IconName { get; set; } = string.Empty;
    /// <summary>Тир (визуальный стиль).</summary>
    public int? Tier { get; set; }
    /// <summary>Порядок отображения внутри серии.</summary>
    public int SortOrder { get; set; }
}

/// <summary>
/// Запрос на частичное обновление. Любое null-поле = не трогать.
/// </summary>
public class UpdateAwardType
{
    /// <summary>Идентификатор обновляемой записи.</summary>
    public Guid Id { get; set; }
    /// <summary>Новое название (null = не менять).</summary>
    public string? Title { get; set; }
    /// <summary>Новое описание (null = не менять).</summary>
    public string? Description { get; set; }
    /// <summary>Новая иконка (null = не менять).</summary>
    public string? IconName { get; set; }
    /// <summary>Новый тир (null = не менять).</summary>
    public int? Tier { get; set; }
    /// <summary>Новый SortOrder (null = не менять).</summary>
    public int? SortOrder { get; set; }
    /// <summary>Новый IsActive (null = не менять).</summary>
    public bool? IsActive { get; set; }
}
