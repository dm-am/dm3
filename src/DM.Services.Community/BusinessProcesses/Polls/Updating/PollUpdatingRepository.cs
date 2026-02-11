using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;
using Poll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;

namespace DM.Services.Community.BusinessProcesses.Polls.Updating;

/// <inheritdoc />
internal class PollUpdatingRepository : MongoCollectionRepository<Poll>, IPollUpdatingRepository
{
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PollUpdatingRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Reading.Poll> UpdatePoll(Guid pollId, string? title, DateTimeOffset? endDate)
    {
        var updates = new List<UpdateDefinition<Poll>>();

        if (title != null)
        {
            updates.Add(Update.Set(p => p.Title, title));
        }

        if (endDate.HasValue)
        {
            updates.Add(Update.Set(p => p.EndDate, endDate.Value));
        }

        if (updates.Count > 0)
        {
            await Collection.UpdateOneAsync(
                Filter.Eq(p => p.Id, pollId),
                Update.Combine(updates));
        }

        var dbPoll = await Collection.Find(Filter.Eq(p => p.Id, pollId)).FirstAsync();
        return _mapper.Map<Reading.Poll>(dbPoll);
    }
}
