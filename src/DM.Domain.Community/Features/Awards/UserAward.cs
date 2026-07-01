using System;
using DM.Domain.Core.Dto;

namespace DM.Domain.Community.Features.Awards;

/// <summary>
/// Награда, выданная конкретному пользователю в рамках серии конкурса.
/// Note и AwardedBy не светятся в публичном API — описание уже есть на типе,
/// модератор-выдавший хранится для audit на уровне БД, но не выдается клиенту.
/// </summary>
public class UserAward
{
    /// <summary>Идентификатор записи о выдаче.</summary>
    public Guid Id { get; set; }

    /// <summary>Пользователь, которому выдана награда.</summary>
    public GeneralUser User { get; set; } = null!;

    /// <summary>Тип награды (запись из каталога).</summary>
    public AwardType Type { get; set; } = null!;

    /// <summary>Серия конкурса, в рамках которой выдана награда (опционально).</summary>
    public ContestSeries? ContestSeries { get; set; }

    /// <summary>
    /// Ссылка на форумный топик с работой, за которую получена награда.
    /// Опциональна — best_critic / guesser не привязаны к конкретной работе.
    /// </summary>
    public string? WorkUrl { get; set; }

    /// <summary>Момент выдачи (UTC).</summary>
    public DateTimeOffset AwardedUtc { get; set; }
}

/// <summary>Запрос на выдачу награды пользователю.</summary>
public class CreateUserAward
{
    /// <summary>Кому выдаем.</summary>
    public Guid UserId { get; set; }
    /// <summary>Тип награды из каталога.</summary>
    public Guid AwardTypeId { get; set; }
    /// <summary>Серия конкурса (опционально для будущих внеконкурсных наград).</summary>
    public Guid? ContestSeriesId { get; set; }
    /// <summary>Ссылка на топик с работой (опционально).</summary>
    public string? WorkUrl { get; set; }
}
