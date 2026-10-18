using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence.RelationalStorage;
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
        // Everything the edit does lives inside the block: the read the reset
        // decision is made on has to happen under the lock, and a strategy retry
        // clears the change tracker, so modifications applied outside would be
        // dropped on the second attempt.
        await RetryableWrite.Run(_dbContext, async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            // The poll row first, and the flag comes back with the lock rather
            // than being read again afterwards. Whether the votes are reset is a
            // decision about the flag as it stands now, and it used to be made on
            // a row read with no lock at all: a vote committed between that read
            // and the delete outlived the reset it was supposed to be cleared by,
            // and one committed just after it was deleted by a reset the voter's
            // ballot never showed. Vote holds the same row FOR SHARE, so the two
            // orderings are now the only ones: the vote is already here and gets
            // deleted, or it waits for this commit and lands on a poll that is
            // already public.
            //
            // A list rather than a single value, because "no rows" is the poll
            // that is not there and false is a poll that is public.
            var locked = await _dbContext.Database
                .SqlQueryRaw<bool>(
                    """SELECT "IsAnonymous" AS "Value" FROM "Polls" WHERE "PollId" = {0} FOR UPDATE""",
                    pollId)
                .ToListAsync();
            if (locked.Count == 0)
            {
                throw new InvalidOperationException($"Poll {pollId} does not exist");
            }

            var wasAnonymous = locked[0];

            // The row alone. Its options and every vote cast on them are a read
            // of unbounded size, and holding a write lock while it runs makes
            // every voter wait for it; the answer this method returns is read
            // after the commit, where the wait costs nobody anything.
            var dbPoll = await _dbContext.Polls
                .TagWith("DM.Community.Polls.Edited")
                .FirstAsync(p => p.PollId == pollId);

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

            // Off the locked value, not off the loaded row: what the reset turns
            // on is the state no other writer can move until this commit.
            var resetsVotes = isAnonymous.HasValue && wasAnonymous && !isAnonymous.Value;
            if (isAnonymous.HasValue)
            {
                dbPoll.IsAnonymous = isAnonymous.Value;
            }

            if (resetsVotes)
            {
                // Reset votes when changing from Anonymous to Public: an
                // anonymous ballot cannot be republished with names on it.
                // Server-side delete rather than the loaded graph, because the
                // vote writes bypass the change tracker and the graph only holds
                // what this context happened to read.
                await _dbContext.PollVotes
                    .Where(v => v.PollId == pollId)
                    .ExecuteDeleteAsync();
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
        });

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

        // The poll row is read FOR SHARE so that the wait is asked for here
        // rather than inherited: the referential check on "PollId" takes FOR KEY
        // SHARE on the same row anyway, and the reset's FOR UPDATE refuses that
        // one just as well, but a lock taken by a constraint is a side effect and
        // not a statement about ordering. Either way a vote cast while an
        // anonymous ballot is being cleared queues behind the reset instead of
        // landing inside it, and two voters never wait on each other, shared
        // locks not conflicting.
        //
        // The lock and the insert are one statement, so there is nothing here for
        // a transaction to hold together and nothing for the execution strategy
        // to replay as a unit: a single statement is its own transaction, and the
        // strategy retries it on its own.
        var inserted = await _dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PollVotes" ("PollId", "UserId", "PollOptionId", "VotedUtc")
            SELECT {pollId}, {userId}, {optionId}, {votedUtc}
            FROM "Polls"
            WHERE "PollId" = {pollId}
            FOR SHARE
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
