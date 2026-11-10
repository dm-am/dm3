using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Publications;

public interface IPublicationRepository
{
    Task<int> CountPublications(Guid blogId, Guid? rubricId, CancellationToken ct = default);

    Task<IEnumerable<Publication>> GetPublications(Guid blogId, Guid? rubricId, PagingData paging,
        CancellationToken ct = default);

    Task<Publication?> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get the user's most-liked publication across every blog they author.
    /// Returns null when the user has no publications at all.
    /// <paramref name="authorIds" /> is the author, or every removed account for
    /// the profile of "Аккаунт удален".
    /// </summary>
    Task<Publication?> GetBestUserPublication(IReadOnlyCollection<Guid> authorIds, CancellationToken ct = default);

    Task<Publication> CreatePublication(CreatePublicationEntity entity, CancellationToken ct = default);

    Task<Publication> UpdatePublication(UpdatePublicationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete publication (soft delete)
    /// </summary>
    Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default);
}
