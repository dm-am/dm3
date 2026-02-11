using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;
using DbPoll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;

namespace DM.Services.Community.BusinessProcesses.Polls.Reading;

/// <inheritdoc />
internal class PollReadingRepository : MongoCollectionRepository<DbPoll>, IPollReadingRepository
{
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PollReadingRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<long> Count(DateTimeOffset? activeUntil)
    {
        var filter = Filter.Eq(p => p.IsRemoved, false);
        if (activeUntil.HasValue)
        {
            filter &= Filter.Gte(p => p.EndDate, activeUntil.Value.UtcDateTime);
        }
        return Collection.CountDocumentsAsync(filter);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Poll>> Get(DateTimeOffset? activeUntil, PagingData pagingData)
    {
        var filter = Filter.Eq(p => p.IsRemoved, false);
        if (activeUntil.HasValue)
        {
            filter &= Filter.Gte(p => p.EndDate, activeUntil.Value.UtcDateTime);
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
}