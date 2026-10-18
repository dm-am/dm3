using System;
using DM.Domain.Core.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Digests;
using Microsoft.EntityFrameworkCore;
using TopicEntity = DM.Infrastructure.Persistence.Entities.Forum.Topic;

namespace DM.Infrastructure.Persistence.Repositories.Forum;

/// <inheritdoc />
internal class PeriodDigestRepository : IPeriodDigestRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IGuidFactory _guidFactory;

    public PeriodDigestRepository(DmDbContext dbContext, IGuidFactory guidFactory)
    {
        _dbContext = dbContext;
        _guidFactory = guidFactory;
    }

    /// <inheritdoc />
    public Task<bool> DigestExists(int year, int? month, CancellationToken cancellationToken = default) =>
        _dbContext.PeriodDigestTopics
            .TagWith("DM.Forum.PeriodDigestExists")
            .AnyAsync(d => d.Year == year && d.Month == month, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TryClaimPeriod(
        Guid topicId,
        int year,
        int? month,
        DateTimeOffset recordedUtc,
        CancellationToken cancellationToken = default)
    {
        // One statement decides the race, and the caller writes nothing until it
        // has won: the topic the marker names is created afterwards. Claimed the
        // other way round, the loser had a topic on its hands and had to take it
        // back - a delete the board's own foreign key to its last topic refuses,
        // and a count it left one too high either way.
        //
        // No conflict target: the two partial unique indexes of the table are the
        // monthly period and the yearly one, and a row is only ever one of the
        // two. Naming a target would mean naming both and telling them apart here.
        var claimed = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PeriodDigestTopics" ("PeriodDigestTopicId", "Year", "Month", "TopicId", "CreatedUtc")
            VALUES ({_guidFactory.Create()}, {year}, {month}, {topicId}, {recordedUtc})
            ON CONFLICT DO NOTHING
            """, cancellationToken);

        return claimed == 1;
    }

    /// <inheritdoc />
    public Task<int> ReleasePeriod(Guid topicId, CancellationToken cancellationToken = default) =>
        // By the identifier the claim was made under rather than by the period: it
        // is the one thing the caller knows belongs to its own claim, and a delete
        // by year and month would take a row another instance is standing on.
        //
        // And only while the topic is absent. Creation commits the topic and then
        // reads it back outside the retry, so a refusal past that commit reaches
        // the caller with the digest already posted: giving the period back there
        // would let the next pass post it a second time. The condition is on the
        // statement rather than on a read before it, because the two would race.
        _dbContext.PeriodDigestTopics
            .Where(d => d.TopicId == topicId &&
                        !_dbContext.Topics.Any(t => t.TopicId == topicId))
            .ExecuteDeleteAsync(cancellationToken);

    /// <inheritdoc />
    public Task DateTopicAtPeriodEnd(
        Guid topicId, DateTimeOffset periodEndUtc, CancellationToken cancellationToken = default) =>
        // Set-based rather than through the tracker: the topic was created a
        // moment earlier by another repository, over the same context, and a
        // tracked copy read back here would carry whatever that path left on it.
        _dbContext.Set<TopicEntity>()
            .Where(t => t.TopicId == topicId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedUtc, periodEndUtc), cancellationToken);
}
