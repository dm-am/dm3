using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Publications;

/// <summary>
/// Service for publication operations
/// </summary>
public interface IPublicationService
{
    /// <summary>
    /// Get publications for a blog
    /// </summary>
    Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default);

    /// <summary>
    /// Get publication by ID
    /// </summary>
    Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get a user's best (most-liked, published-only) publication across
    /// every blog they author. Used by the profile "Blogs" tab. Returns
    /// null when the user has no published publications.
    /// </summary>
    Task<Publication?> GetBestUserPublication(string username, CancellationToken ct = default);

    /// <summary>
    /// Create a new publication
    /// </summary>
    Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default);

    /// <summary>
    /// Update publication
    /// </summary>
    Task<Publication> UpdatePublication(UpdatePublication updatePublication, CancellationToken ct = default);

    /// <summary>
    /// Delete publication
    /// </summary>
    Task DeletePublication(Guid publicationId, CancellationToken ct = default);
}
