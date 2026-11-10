using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;

namespace DM.Domain.Blog.Features.Blogs;

// Whether the viewer reads a blog: by the visibility its own reads and its discussion ask.
internal class BlogPlaceReading : IPlaceReading
{
    private readonly IBlogService _blogs;

    public BlogPlaceReading(IBlogService blogs)
    {
        _blogs = blogs;
    }

    /// <inheritdoc />
    public ReadablePlaceKind Kind => ReadablePlaceKind.Blog;

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> ReadableAsync(
        IReadOnlyCollection<Guid> placeIds, CancellationToken ct = default)
    {
        var readable = new HashSet<Guid>();
        foreach (var blogId in placeIds.Distinct())
        {
            if (await _blogs.IsVisibleToViewerAsync(blogId, ct))
            {
                readable.Add(blogId);
            }
        }

        return readable;
    }
}
