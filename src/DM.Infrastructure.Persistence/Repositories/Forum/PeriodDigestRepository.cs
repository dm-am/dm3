using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Digests;
using DM.Infrastructure.Persistence.Entities.Community;
using Microsoft.EntityFrameworkCore;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class PeriodDigestRepository : IPeriodDigestRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc cref="PeriodDigestRepository" />
    public PeriodDigestRepository(DmDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public Task<bool> DigestExists(int year, int? month, CancellationToken cancellationToken = default) =>
        _dbContext.PeriodDigestTopics
            .TagWith("DM.Forum.PeriodDigestExists")
            .AnyAsync(d => d.Year == year && d.Month == month, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryRecordDigest(
        Guid topicId,
        int year,
        int? month,
        DateTimeOffset periodEndUtc,
        DateTimeOffset recordedUtc,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var topic = await _dbContext.Set<TopicEntity>()
                .FirstAsync(t => t.TopicId == topicId, cancellationToken);
            topic.CreatedUtc = periodEndUtc;

            _dbContext.PeriodDigestTopics.Add(new PeriodDigestTopic
            {
                PeriodDigestTopicId = Guid.NewGuid(),
                Year = year,
                Month = month,
                TopicId = topicId,
                CreatedUtc = recordedUtc,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            // Unique-index race with another instance, or a marker failure.
            // Compensate: drop the just-created topic (its marker never landed),
            // so no duplicate digest ever survives.
            _dbContext.ChangeTracker.Clear();
            await _dbContext.Set<TopicEntity>()
                .Where(t => t.TopicId == topicId)
                .ExecuteDeleteAsync(cancellationToken);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task RefreshLastTopic(Guid boardId, CancellationToken cancellationToken = default)
    {
        // A direct UPDATE from the actual freshest topic: set-based, immune to
        // whatever the context has tracked.
        var latest = await _dbContext.Set<TopicEntity>()
            .TagWith("DM.Forum.BoardLatestTopic")
            .Where(t => t.BoardId == boardId && !t.IsRemoved)
            .OrderByDescending(t => t.CreatedUtc)
            .Select(t => new
            {
                t.TopicId,
                t.TopicNumber,
                t.Title,
                t.AuthorId,
                t.CreatedUtc,
            })
            .FirstAsync(cancellationToken);

        await _dbContext.Boards
            .Where(b => b.BoardId == boardId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.LastTopicId, latest.TopicId)
                .SetProperty(b => b.LastTopicNumber, latest.TopicNumber)
                .SetProperty(b => b.LastTopicTitle, latest.Title)
                .SetProperty(b => b.LastTopicAuthorId, latest.AuthorId)
                .SetProperty(b => b.LastTopicCreatedUtc, latest.CreatedUtc),
                cancellationToken);
    }
}
