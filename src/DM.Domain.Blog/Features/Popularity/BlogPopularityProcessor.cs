using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Configuration;

namespace DM.Domain.Blog.Features.Popularity;

/// <inheritdoc />
internal class BlogPopularityProcessor : IBlogPopularityProcessor
{
    private readonly IBlogPopularityRepository _repository;

    public BlogPopularityProcessor(IBlogPopularityRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async Task<(int Updated, int Total)> UpdateScoresAsync(
        DateTimeOffset now, CancellationToken ct = default)
    {
        var blogIds = await _repository.GetScorableBlogIds(ct);
        if (blogIds.Count == 0)
        {
            return (0, 0);
        }

        // "Active" is one product decision for the whole server, and the games
        // side of the same score reads the same constant.
        var activeSince = now - ActivityPolicy.ActivePeriod;

        var readers = await _repository.CountActiveReaders(blogIds, activeSince, ct);

        var scores = blogIds.ToDictionary(id => id, id => readers.GetValueOrDefault(id));

        var updated = await _repository.ApplyScores(scores, now, ct);
        return (updated, blogIds.Count);
    }
}
