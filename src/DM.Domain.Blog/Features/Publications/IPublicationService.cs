using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;

namespace DM.Domain.Blog.Features.Publications;

public interface IPublicationService
{
    Task<(IEnumerable<Publication> publications, PagingResult paging)> GetPublications(
        Guid blogId, Guid? rubricId, PagingQuery query, CancellationToken ct = default);

    Task<Publication> GetPublication(Guid publicationId, CancellationToken ct = default);

    /// <summary>
    /// Get a user's best (most-liked, published-only) publication across
    /// every blog they author. Used by the profile "Blogs" tab. Returns
    /// null when the user has no published publications.
    /// </summary>
    Task<Publication?> GetBestUserPublication(string username, CancellationToken ct = default);

    Task<Publication> CreatePublication(CreatePublication createPublication, CancellationToken ct = default);

    Task<Publication> UpdatePublication(UpdatePublication updatePublication, CancellationToken ct = default);

    Task DeletePublication(Guid publicationId, CancellationToken ct = default);
}
