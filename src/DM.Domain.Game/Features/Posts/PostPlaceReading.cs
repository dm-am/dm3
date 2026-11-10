using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Game.Features.Posts;

// The reviews of a post are read where the post is.
internal class PostPlaceReading : IPlaceReading
{
    private readonly IPostService _posts;

    public PostPlaceReading(IPostService posts)
    {
        _posts = posts;
    }

    /// <inheritdoc />
    public ReadablePlaceKind Kind => ReadablePlaceKind.Post;

    /// <inheritdoc />
    public Task<IReadOnlySet<Guid>> ReadableAsync(IReadOnlyCollection<Guid> placeIds, CancellationToken ct = default) =>
        PlaceReading.ThroughOwnRead(placeIds, (postId, token) => _posts.GetAsync(postId, token), ct);
}
