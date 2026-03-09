using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Dto;
using DM.Domain.Community.Features.Polls;
using DM.Infrastructure.Persistence.MongoIntegration;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using DbPoll = DM.Infrastructure.Persistence.Entities.Forum.Poll;
using DbPollOption = DM.Infrastructure.Persistence.Entities.Forum.PollOption;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class PollRepository : MongoCollectionRepository<DbPoll>, IPollRepository
{
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PollRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        _mapper = mapper;
    }

    // ═══ READ ═══

    /// <inheritdoc />
    public Task<long> Count(DateTimeOffset? activeAt)
    {
        var filter = Filter.Eq(p => p.IsRemoved, false);
        if (activeAt.HasValue)
        {
            filter &= Filter.Gte(p => p.EndDate, activeAt.Value.UtcDateTime);
        }
        return Collection.CountDocumentsAsync(filter);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Poll>> Get(DateTimeOffset? activeAt, PagingData pagingData)
    {
        var filter = Filter.Eq(p => p.IsRemoved, false);
        if (activeAt.HasValue)
        {
            filter &= Filter.Gte(p => p.EndDate, activeAt.Value.UtcDateTime);
        }

        var dbPolls = await Collection
            .Find(filter)
            .Sort(Sort.Descending(p => p.StartDate))
            .Skip(pagingData.Skip)
            .Limit(pagingData.Take)
            .ToListAsync();
        return dbPolls.Select(_mapper.Map<Poll>);
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
            StartDate = poll.StartDate,
            EndDate = poll.EndDate,
            Global = poll.Global,
            Title = poll.Title,
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
    public new async Task<Poll> Update(Guid pollId, string? title, DateTimeOffset? endDate)
    {
        var update = Builders<DbPoll>.Update;
        var updates = new List<UpdateDefinition<DbPoll>>();

        if (title != null)
        {
            updates.Add(update.Set(p => p.Title, title));
        }

        if (endDate.HasValue)
        {
            updates.Add(update.Set(p => p.EndDate, endDate.Value));
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
    public async Task<Poll> Vote(Guid pollId, Guid optionId, Guid userId)
    {
        var dbPoll = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(p => p.Id, pollId) &
            Filter.ElemMatch(p => p.Options, o => o.Id == optionId),
            Builders<DbPoll>.Update.AddToSet(u => u.Options.FirstMatchingElement().UserIds, userId),
            new FindOneAndUpdateOptions<DbPoll>
            {
                ReturnDocument = ReturnDocument.After
            });
        return _mapper.Map<Poll>(dbPoll);
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
