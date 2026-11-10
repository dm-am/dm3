using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Blog.Features.Publications;

// Whether the viewer reads a publication: by the read of the publication itself, the one its
// discussion is read through.
internal class PublicationPlaceReading : IPlaceReading
{
    private readonly IPublicationService _publications;

    public PublicationPlaceReading(IPublicationService publications)
    {
        _publications = publications;
    }

    /// <inheritdoc />
    public ReadablePlaceKind Kind => ReadablePlaceKind.Publication;

    /// <inheritdoc />
    public Task<IReadOnlySet<Guid>> ReadableAsync(IReadOnlyCollection<Guid> placeIds, CancellationToken ct = default) =>
        PlaceReading.ThroughOwnRead(
            placeIds, (publicationId, token) => _publications.GetPublication(publicationId, token), ct);
}
