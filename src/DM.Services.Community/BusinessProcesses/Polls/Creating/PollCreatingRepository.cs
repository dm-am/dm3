using System.Threading.Tasks;
using AutoMapper;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;
using Poll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;

namespace DM.Services.Community.BusinessProcesses.Polls.Creating;

/// <inheritdoc />
internal class PollCreatingRepository : MongoCollectionRepository<Poll>, IPollCreatingRepository
{
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PollCreatingRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Reading.Poll> Create(Poll poll)
    {
        await Collection.InsertOneAsync(poll);
        var dbPoll = await Collection.Find(Filter.Eq(p => p.Id, poll.Id))
            .FirstAsync();
        return _mapper.Map<Reading.Poll>(dbPoll);
    }
}