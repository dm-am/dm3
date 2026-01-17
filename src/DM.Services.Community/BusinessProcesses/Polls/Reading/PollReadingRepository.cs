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
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PollReadingRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public Task<long> Count(DateTimeOffset? activeUntil) =>
        Collection.CountDocumentsAsync(activeUntil.HasValue
            ? Filter.Gte(p => p.EndDate, activeUntil.Value.UtcDateTime)
            : Filter.Empty);

    /// <inheritdoc />
    public async Task<IEnumerable<Poll>> Get(DateTimeOffset? activeUntil, PagingData pagingData)
    {
        var dbPolls = await Collection
            .Find(activeUntil.HasValue
                ? Filter.Gte(p => p.EndDate, activeUntil.Value.UtcDateTime)
                : Filter.Empty)
            .Sort(Sort.Descending(p => p.StartDate))
            .Skip(pagingData.Skip)
            .Limit(pagingData.Take)
            .ToListAsync();
        return dbPolls.Select(mapper.Map<Poll>);
    }

    /// <inheritdoc />
    public async Task<Poll> Get(Guid id)
    {
        var dbPoll = await Collection
            .Find(Filter.Eq(p => p.Id, id))
            .FirstOrDefaultAsync();
        return mapper.Map<Poll>(dbPoll);
    }
}