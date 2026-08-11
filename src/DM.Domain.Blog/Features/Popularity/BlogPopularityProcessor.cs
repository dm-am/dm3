using System;
using System.Collections.Generic;
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
        DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var blogIds = await _repository.GetScorableBlogIds(cancellationToken);
        if (blogIds.Count == 0)
        {
            return (0, 0);
        }

        // "Active" is one product decision for the whole server, and the games
        // side of the same score reads the same constant.
        var activeSince = now - ActivityPolicy.ActivePeriod;

        var readers = await _repository.CountActiveReaders(blogIds, activeSince, cancellationToken);

        var scores = new Dictionary<Guid, int>(blogIds.Count);
        foreach (var blogId in blogIds)
        {
            readers.TryGetValue(blogId, out var readerCount);
            scores[blogId] = readerCount;
        }

        var updated = await _repository.ApplyScores(scores, now, cancellationToken);
        return (updated, blogIds.Count);
    }
}
