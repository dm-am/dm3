using DM.Domain.Core.Dto;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// Search / sort / paging для GET-endpoint'ов рекомендаций
/// (как полученных, так и написанных). Контракт зеркалит
/// FE-композабл <c>useReviewsFilter</c>: дефолтная сортировка =
/// дата desc, поиск необязательный.
/// </summary>
public class UserEndorsementsQuery : PagingQuery
{
    /// <summary>
    /// Подстрочный поиск (case-insensitive) по тексту рекомендации,
    /// имени автора и имени получателя. Пусто = без поиска.
    /// </summary>
    public string? Search { get; set; }

    /// <summary>Поле сортировки: <c>created</c> или <c>author</c>. Дефолт — created.</summary>
    public string? SortBy { get; set; }

    /// <summary>Направление сортировки: <c>asc</c> или <c>desc</c>. Дефолт — desc.</summary>
    public string? SortOrder { get; set; }
}
