using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// Publication repository
/// </summary>
public interface IPublicationRepository
{
    // === READ ===

    /// <summary>
    /// Count publications for a blog
    /// </summary>
    Task<int> CountPublications(Guid blogId, Guid? rubricId, bool includeUnpublished, CancellationToken ct = default);

    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<IEnumerable<Publication>> GetPublications(
        Guid blogId, Guid? rubricId, bool includeUnpublished, PagingData paging, CancellationToken ct = default);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<Publication?> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get the user's most-liked publication across every blog they author.
    /// Published-only — drafts and unpublished posts are excluded so the
    /// profile widget never shows content the user hasn't released yet.
    /// Returns null when the user has no published publications at all.
    /// </summary>
    Task<Publication?> GetBestUserPublication(Guid authorId, CancellationToken ct = default);

    // === WRITE ===

    /// <summary>
    /// Create a new publication
    /// </summary>
    /// <param name="entity">Publication creation entity</param>
    /// <param name="ct">Cancellation token</param>
    Task<Publication> CreatePublication(CreatePublicationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Publication> UpdatePublication(UpdatePublicationEntity entity, CancellationToken ct = default);

    /// <summary>
    /// Delete publication (soft delete)
    /// </summary>
    Task DeletePublication(Guid publicationId, Guid deletedByUserId, CancellationToken ct = default);
}
