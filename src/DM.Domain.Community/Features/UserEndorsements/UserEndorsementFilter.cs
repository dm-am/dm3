using System;

namespace DM.Domain.Community.Features.UserEndorsements;

/// <summary>
/// Filter / search / sort параметры для рекомендаций.
/// Используется одинаково для «полученных» и «написанных» listing'ов —
/// различие только в том, какое из <see cref="AuthorId"/> / <see cref="RecipientId"/>
/// устанавливается контроллером.
/// </summary>
public class UserEndorsementFilter
{
    /// <summary>Filter by endorsement author.</summary>
    public Guid? AuthorId { get; set; }

    /// <summary>Filter by endorsement recipient (target user).</summary>
    public Guid? RecipientId { get; set; }

    /// <summary>
    /// Подстрочный поиск (case-insensitive) по тексту рекомендации,
    /// имени автора и имени получателя. Пусто = без поиска.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Поле сортировки: <c>created</c> (по дате) или <c>author</c>
    /// (по имени автора, алфавитно). Дефолт — created.
    /// </summary>
    public string? SortBy { get; set; }

    /// <summary>
    /// Направление сортировки: <c>asc</c> или <c>desc</c>. Дефолт — desc.
    /// </summary>
    public string? SortOrder { get; set; }
}
