using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence.Shared.Queries;
using Microsoft.EntityFrameworkCore;
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Community.PollOption;
using DbPollVote = DM.Infrastructure.Persistence.Entities.Community.PollVote;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class PollRepository : IPollRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public async Task<long> Count(PollsQuery query)
    {
        return await BuildFilter(query).CountAsync();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Poll>> Get(PollsQuery query, PagingData pagingData)
    {
        var dbPolls = await BuildSort(BuildFilter(query), query)
            .Skip(pagingData.Skip)
            .Take(pagingData.Take)
            .Include(p => p.Options)
            .ThenInclude(o => o.Votes)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync();
        return dbPolls.Select(PollMapper.ToPoll);
    }

    private IQueryable<DbPoll> BuildFilter(PollsQuery? query)
    {
        // The global soft-delete filter already hides removed polls; everything
        // else the caller asked for is composed below.
        var polls = _dbContext.Polls
            .TagWith("DM.Community.Polls")
            .AsQueryable();

        if (query == null)
            return polls;

        // Status is a position relative to now, so it is a comparison on the two
        // dates rather than a stored field. The window is Poll.StatusAt in the
        // community domain; these three arms restate it and have to move with it.
        // Every member is spelled out and there is no arm for anything else: the
        // binder refuses a word outside the vocabulary, where the string form
        // used to fall past all three comparisons and answer with every poll.
        var now = _dateTimeProvider.Now;
        polls = query.Status switch
        {
            PollStatus.Pending => polls.Where(p => p.StartsUtc > now),
            PollStatus.Active => polls.Where(p => p.StartsUtc <= now && p.EndsUtc > now),
            PollStatus.Closed => polls.Where(p => p.EndsUtc <= now),
            _ => polls
        };

        // Search filter (Title + Details)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePatterns.Contains(query.Search);
            polls = polls.Where(p =>
                EF.Functions.ILike(p.Title, pattern, "\\") ||
                (p.Details != null && EF.Functions.ILike(p.Details, pattern, "\\")));
        }

        // Date range filters for StartsUtc
        if (query.StartsFromUtc.HasValue)
        {
            polls = polls.Where(p => p.StartsUtc >= query.StartsFromUtc.Value);
        }
        if (query.StartsToUtc.HasValue)
        {
            polls = polls.WhereAtOrBefore(p => p.StartsUtc, query.StartsToUtc.Value);
        }

        // Date range filters for EndsUtc
        if (query.EndsFromUtc.HasValue)
        {
            polls = polls.Where(p => p.EndsUtc >= query.EndsFromUtc.Value);
        }
        if (query.EndsToUtc.HasValue)
        {
            polls = polls.WhereAtOrBefore(p => p.EndsUtc, query.EndsToUtc.Value);
        }

        // Anonymous/Public filter
        if (query.IsAnonymous.HasValue)
        {
            polls = polls.Where(p => p.IsAnonymous == query.IsAnonymous.Value);
        }

        return polls;
    }

    private IOrderedQueryable<DbPoll> BuildSort(IQueryable<DbPoll> polls, PollsQuery? query)
    {
        if (query == null)
            return polls.OrderBy(p => p.StartsUtc);

        var isDesc = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        // Status order is derived from the current moment, the same ladder as
        // Poll.StatusAt: Pending (0) -> Active (1) -> Closed (2). A CASE
        // expression in SQL replaces the aggregation pipeline the document
        // store needed for the same sort.
        if (string.Equals(query.SortBy, "status", StringComparison.OrdinalIgnoreCase))
        {
            var now = _dateTimeProvider.Now;
            return isDesc
                ? polls.OrderByDescending(p => p.StartsUtc > now ? 0 : p.EndsUtc > now ? 1 : 2)
                    .ThenByDescending(p => p.StartsUtc)
                : polls.OrderBy(p => p.StartsUtc > now ? 0 : p.EndsUtc > now ? 1 : 2)
                    .ThenByDescending(p => p.StartsUtc);
        }

        return query.SortBy?.ToLowerInvariant() switch
        {
            "ends" => isDesc ? polls.OrderByDescending(p => p.EndsUtc) : polls.OrderBy(p => p.EndsUtc),
            // Default to StartsUtc
            _ => isDesc ? polls.OrderByDescending(p => p.StartsUtc) : polls.OrderBy(p => p.StartsUtc),
        };
    }

    /// <inheritdoc />
    public async Task<Poll> Get(Guid id)
    {
        var dbPoll = await LoadPoll(id);
        return dbPoll.ToPoll();
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<Poll> Create(CreatePollEntity poll)
    {
        var dbPoll = new DbPoll
        {
            PollId = poll.Id,
            StartsUtc = new DateTimeOffset(poll.StartsUtc, TimeSpan.Zero),
            EndsUtc = new DateTimeOffset(poll.EndsUtc, TimeSpan.Zero),
            Title = poll.Title,
            Details = poll.Details,
            IsAnonymous = poll.IsAnonymous,
            IsRemoved = false,
            // The order of the options used to be the order of the document's
            // array; the column spells it out.
            Options = poll.Options.Select((o, index) => new DbPollOption
            {
                PollOptionId = o.Id,
                PollId = poll.Id,
                Text = o.Text,
                Order = index
            }).ToList()
        };

        _dbContext.Polls.Add(dbPoll);
        await _dbContext.SaveChangesAsync();

        return dbPoll.ToPoll();
    }

    /// <inheritdoc />
    public async Task<Poll> Update(Guid pollId, string? title, string? details,
        DateTimeOffset? startDate, DateTimeOffset? endDate, bool? isAnonymous)
    {
        var dbPoll = await LoadPoll(pollId, tracked: true) ?? throw new InvalidOperationException(
            $"Poll {pollId} does not exist");

        if (title != null)
        {
            dbPoll.Title = title;
        }

        if (details != null)
        {
            dbPoll.Details = details == string.Empty ? null : details;
        }

        if (startDate.HasValue)
        {
            dbPoll.StartsUtc = startDate.Value.ToUniversalTime();
        }

        if (endDate.HasValue)
        {
            dbPoll.EndsUtc = endDate.Value.ToUniversalTime();
        }

        var resetsVotes = isAnonymous.HasValue && dbPoll.IsAnonymous && !isAnonymous.Value;
        if (isAnonymous.HasValue)
        {
            dbPoll.IsAnonymous = isAnonymous.Value;
        }

        if (resetsVotes)
        {
            // Reset votes when changing from Anonymous to Public. ExecuteDelete
            // by PollId under one transaction with the flag save: deleting the
            // loaded graph left a window where a vote inserted concurrently by
            // the raw ON CONFLICT path survived "reset all votes" (review of
            // W1.1). Server-side delete sees every row, and the transaction
            // keeps the pair both-or-neither. The tracked modifications survive
            // a strategy retry as they are - nothing here re-adds entities.
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await _dbContext.Database.BeginTransactionAsync();
                await _dbContext.PollVotes
                    .Where(v => v.PollId == pollId)
                    .ExecuteDeleteAsync();
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            });
        }
        else
        {
            await _dbContext.SaveChangesAsync();
        }

        return (await LoadPoll(pollId)).ToPoll();
    }

    /// <inheritdoc />
    public Task Delete(Guid pollId) =>
        _dbContext.Polls
            .Where(p => p.PollId == pollId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsRemoved, true));

    // ═══ VOTING ═══

    /// <inheritdoc />
    /// <remarks>
    /// One voter, one option, enforced by the write itself: the primary key of
    /// PollVotes is (PollId, UserId), and the insert is ON CONFLICT DO NOTHING.
    /// Two requests arriving together both used to pass a preceding check; here
    /// the server keeps one row and reports the other insert as touching
    /// nothing, and the caller is told.
    ///
    /// Changing one's mind goes through Unvote first — the endpoint for it
    /// exists.
    /// </remarks>
    public async Task<Poll?> Vote(Guid pollId, Guid optionId, Guid userId)
    {
        // The option has to belong to the poll: the key that holds "one voter,
        // one option" cannot also say which options are on the ballot.
        var optionExists = await _dbContext.PollOptions
            .TagWith("DM.Community.Polls.VoteOption")
            .AnyAsync(o => o.PollOptionId == optionId && o.PollId == pollId);
        if (!optionExists)
        {
            return null;
        }

        var votedUtc = _dateTimeProvider.Now;
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PollVotes" ("PollId", "UserId", "PollOptionId", "VotedUtc")
            VALUES ({pollId}, {userId}, {optionId}, {votedUtc})
            ON CONFLICT ("PollId", "UserId") DO NOTHING
            """);

        return inserted == 0 ? null : (await LoadPoll(pollId)).ToPoll();
    }

    /// <inheritdoc />
    public async Task<Poll> Unvote(Guid pollId, Guid userId)
    {
        await _dbContext.PollVotes
            .Where(v => v.PollId == pollId && v.UserId == userId)
            .ExecuteDeleteAsync();

        return (await LoadPoll(pollId)).ToPoll();
    }

    /// <summary>
    /// The poll with its ballot. Untracked by default: the vote writes bypass
    /// the change tracker (raw ON CONFLICT, ExecuteDelete), so a tracked read
    /// after one of them would merge in stale tracked votes. Only Update, which
    /// edits through the tracker, asks for the tracked shape.
    /// </summary>
    private Task<DbPoll?> LoadPoll(Guid pollId, bool tracked = false)
    {
        var polls = _dbContext.Polls
            .TagWith("DM.Community.Polls.Load")
            .Where(p => p.PollId == pollId)
            .Include(p => p.Options)
            .ThenInclude(o => o.Votes)
            .AsSplitQuery();
        return (tracked ? polls : polls.AsNoTracking()).FirstOrDefaultAsync();
    }
}
