using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Shared.Queries;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using DbPoll = DM.Infrastructure.Persistence.Entities.Community.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Community.PollOption;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class PollRepository : MongoCollectionRepository<DbPoll>, IPollRepository
{
    private readonly IMapper _mapper;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public PollRepository(
        DmMongoClient client,
        IMapper mapper,
        IDateTimeProvider dateTimeProvider) : base(client)
    {
        _mapper = mapper;
        _dateTimeProvider = dateTimeProvider;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<long> Count(PollsQuery query)
    {
        return Collection.CountDocumentsAsync(BuildFilter(query));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Poll>> Get(PollsQuery query, PagingData pagingData)
    {
        var filter = BuildFilter(query);

        // Status sort requires aggregation to compute status order
        if (string.Equals(query?.SortBy, "status", StringComparison.OrdinalIgnoreCase))
        {
            var dbPolls = await GetWithStatusSort(filter, query!, pagingData);
            return dbPolls.Select(_mapper.Map<Poll>);
        }

        var sort = BuildSort(query);
        var dbPollsSimple = await Collection
            .Find(filter)
            .Sort(sort)
            .Skip(pagingData.Skip)
            .Limit(pagingData.Take)
            .ToListAsync();
        return dbPollsSimple.Select(_mapper.Map<Poll>);
    }

    /// <summary>
    /// Get polls with proper status sorting using aggregation.
    /// Status order: Pending (0) → Active (1) → Closed (2)
    /// </summary>
    private async Task<List<DbPoll>> GetWithStatusSort(
        FilterDefinition<DbPoll> filter,
        PollsQuery query,
        PagingData pagingData)
    {
        // Pending/Active/Closed are derived from the current moment, so the one
        // clock the application agrees on decides it. UtcDateTime because the
        // value goes into a BsonArray, which has no conversion from DateTimeOffset.
        var now = _dateTimeProvider.Now.UtcDateTime;
        var isDesc = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        // Build aggregation pipeline with computed statusOrder field. The window
        // is Poll.StatusAt in the community domain, restated here for the same
        // reason as in BuildFilter: the server sorts, so the ladder has to travel
        // as a document.
        // Pending: StartsUtc > now → order 0
        // Active: StartsUtc <= now AND EndsUtc > now → order 1
        // Closed: EndsUtc <= now → order 2
        var pipeline = Collection.Aggregate()
            .Match(filter)
            .AppendStage<BsonDocument>(new BsonDocument("$addFields", new BsonDocument("statusOrder",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$gt", new BsonArray { "$StartsUtc", now }),
                    0, // Pending
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$gt", new BsonArray { "$EndsUtc", now }),
                        1, // Active
                        2  // Closed
                    })
                }))))
            .AppendStage<BsonDocument>(new BsonDocument("$sort", isDesc
                ? new BsonDocument { { "statusOrder", -1 }, { "StartsUtc", -1 } }
                : new BsonDocument { { "statusOrder", 1 }, { "StartsUtc", -1 } }))
            .Skip(pagingData.Skip)
            .Limit(pagingData.Take)
            // Remove computed field before deserializing to DbPoll
            .AppendStage<BsonDocument>(new BsonDocument("$unset", "statusOrder"))
            .As<DbPoll>();

        return await pipeline.ToListAsync();
    }

    private FilterDefinition<DbPoll> BuildFilter(PollsQuery? query)
    {
        var filter = Filter.Eq(p => p.IsRemoved, false);

        if (query == null)
            return filter;

        // Status is a position relative to now, so it is a comparison on the two
        // dates rather than a stored field. The window is Poll.StatusAt in the
        // community domain; a Mongo filter is built and shipped rather than
        // called, so these three arms restate it and have to move with it.
        // Every member is spelled out and there is no arm for anything else: the
        // binder refuses a word outside the vocabulary, where the string form
        // used to fall past all three comparisons and answer with every poll.
        var now = _dateTimeProvider.Now.UtcDateTime;
        filter &= query.Status switch
        {
            PollStatus.Pending => Filter.Gt(p => p.StartsUtc, now),
            PollStatus.Active => Filter.Lte(p => p.StartsUtc, now) & Filter.Gt(p => p.EndsUtc, now),
            PollStatus.Closed => Filter.Lte(p => p.EndsUtc, now),
            _ => Filter.Empty
        };

        // Search filter (Title + Details)
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Escape regex special characters for literal search
            var escapedSearch = Regex.Escape(query.Search);
            var regex = new MongoDB.Bson.BsonRegularExpression(escapedSearch, "i");
            filter &= Filter.Or(
                Filter.Regex(p => p.Title, regex),
                Filter.Regex(p => p.Details, regex));
        }

        // Date range filters for StartsUtc
        if (query.StartsFromUtc.HasValue)
        {
            filter &= Filter.Gte(p => p.StartsUtc, query.StartsFromUtc.Value.UtcDateTime);
        }
        if (query.StartsToUtc.HasValue)
        {
            filter &= DateRangeFilters.AtOrBefore<DbPoll>(p => p.StartsUtc, query.StartsToUtc.Value);
        }

        // Date range filters for EndsUtc
        if (query.EndsFromUtc.HasValue)
        {
            filter &= Filter.Gte(p => p.EndsUtc, query.EndsFromUtc.Value.UtcDateTime);
        }
        if (query.EndsToUtc.HasValue)
        {
            filter &= DateRangeFilters.AtOrBefore<DbPoll>(p => p.EndsUtc, query.EndsToUtc.Value);
        }

        // Anonymous/Public filter
        if (query.IsAnonymous.HasValue)
        {
            filter &= Filter.Eq(p => p.IsAnonymous, query.IsAnonymous.Value);
        }

        return filter;
    }

    private SortDefinition<DbPoll> BuildSort(PollsQuery? query)
    {
        // Note: "status" sort is handled via aggregation
        if (query == null)
            return Sort.Ascending(p => p.StartsUtc);

        var isDesc = string.Equals(query.SortOrder, "desc", StringComparison.OrdinalIgnoreCase);

        return query.SortBy?.ToLowerInvariant() switch
        {
            "starts" => isDesc ? Sort.Descending(p => p.StartsUtc) : Sort.Ascending(p => p.StartsUtc),
            "ends" => isDesc ? Sort.Descending(p => p.EndsUtc) : Sort.Ascending(p => p.EndsUtc),
            // Default to StartsUtc
            _ => isDesc ? Sort.Descending(p => p.StartsUtc) : Sort.Ascending(p => p.StartsUtc),
        };
    }

    /// <inheritdoc />
    public async Task<Poll> Get(Guid id)
    {
        var dbPoll = await Collection
            .Find(Filter.Eq(p => p.Id, id) & Filter.Eq(p => p.IsRemoved, false))
            .FirstOrDefaultAsync();
        return _mapper.Map<Poll>(dbPoll);
    }

    // ═══ WRITE ═══

    /// <inheritdoc />
    public async Task<Poll> Create(CreatePollEntity poll)
    {
        var dbPoll = new DbPoll
        {
            Id = poll.Id,
            StartsUtc = poll.StartsUtc,
            EndsUtc = poll.EndsUtc,
            Title = poll.Title,
            Details = poll.Details,
            IsAnonymous = poll.IsAnonymous,
            IsRemoved = false,
            Options = poll.Options.Select(o => new DbPollOption
            {
                Id = o.Id,
                Text = o.Text,
                UserIds = []
            }).ToList()
        };

        await Collection.InsertOneAsync(dbPoll);
        var createdPoll = await Collection.Find(Filter.Eq(p => p.Id, poll.Id))
            .FirstAsync();
        return _mapper.Map<Poll>(createdPoll);
    }

    /// <inheritdoc />
    public new async Task<Poll> Update(Guid pollId, string? title, string? details,
        DateTimeOffset? startDate, DateTimeOffset? endDate, bool? isAnonymous)
    {
        var currentPoll = await Collection.Find(Filter.Eq(p => p.Id, pollId)).FirstAsync();
        var update = Builders<DbPoll>.Update;
        var updates = new List<UpdateDefinition<DbPoll>>();

        if (title != null)
        {
            updates.Add(update.Set(p => p.Title, title));
        }

        if (details != null)
        {
            updates.Add(update.Set(p => p.Details, details == string.Empty ? null : details));
        }

        if (startDate.HasValue)
        {
            updates.Add(update.Set(p => p.StartsUtc, startDate.Value.UtcDateTime));
        }

        if (endDate.HasValue)
        {
            updates.Add(update.Set(p => p.EndsUtc, endDate.Value.UtcDateTime));
        }

        if (isAnonymous.HasValue)
        {
            // Reset votes when changing from Anonymous to Public
            if (currentPoll.IsAnonymous && !isAnonymous.Value)
            {
                updates.Add(update.Set(p => p.Options,
                    currentPoll.Options.Select(o => new DbPollOption
                    {
                        Id = o.Id,
                        Text = o.Text,
                        UserIds = []
                    }).ToList()));
            }
            updates.Add(update.Set(p => p.IsAnonymous, isAnonymous.Value));
        }

        if (updates.Count > 0)
        {
            await Collection.UpdateOneAsync(
                Filter.Eq(p => p.Id, pollId),
                update.Combine(updates));
        }

        var dbPoll = await Collection.Find(Filter.Eq(p => p.Id, pollId)).FirstAsync();
        return _mapper.Map<Poll>(dbPoll);
    }

    /// <inheritdoc />
    public Task Delete(Guid pollId) =>
        Collection.UpdateOneAsync(
            Filter.Eq(p => p.Id, pollId),
            Builders<DbPoll>.Update.Set(p => p.IsRemoved, true));

    // ═══ VOTING ═══

    /// <inheritdoc />
    /// <remarks>
    /// One voter, one option, enforced by the write itself. The condition is part
    /// of the filter rather than a read before it: two requests arriving together
    /// both passed a preceding check and both landed, and nothing else in the
    /// stack looked. A ballot that already carries this voter matches nothing
    /// here, the update touches no document, and the caller is told.
    ///
    /// Changing one's mind goes through Unvote first — the endpoint for it exists —
    /// because pulling from every option and pushing into one cannot be a single
    /// update: both address the same array path, and the server refuses that.
    /// </remarks>
    public async Task<Poll?> Vote(Guid pollId, Guid optionId, Guid userId)
    {
        var dbPoll = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(p => p.Id, pollId) &
            Filter.ElemMatch(p => p.Options, o => o.Id == optionId) &
            Filter.Not(Filter.ElemMatch(p => p.Options, o => o.UserIds.Contains(userId))),
            Builders<DbPoll>.Update.AddToSet(u => u.Options.FirstMatchingElement().UserIds, userId),
            new FindOneAndUpdateOptions<DbPoll>
            {
                ReturnDocument = ReturnDocument.After
            });
        return dbPoll == null ? null : _mapper.Map<Poll>(dbPoll);
    }

    /// <inheritdoc />
    public async Task<Poll> Unvote(Guid pollId, Guid userId)
    {
        var dbPoll = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(p => p.Id, pollId),
            Builders<DbPoll>.Update.PullAll("Options.$[].UserIds", new[] { userId }),
            new FindOneAndUpdateOptions<DbPoll>
            {
                ReturnDocument = ReturnDocument.After
            });
        return _mapper.Map<Poll>(dbPoll);
    }
}
