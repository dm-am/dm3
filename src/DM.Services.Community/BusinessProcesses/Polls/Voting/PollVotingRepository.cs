using System;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Community.BusinessProcesses.Polls.Reading;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using DbPoll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;

namespace DM.Services.Community.BusinessProcesses.Polls.Voting;

/// <inheritdoc />
internal class PollVotingRepository : MongoCollectionRepository<DbPoll>, IPollVotingRepository
{
    private readonly IMapper mapper;

    /// <inheritdoc />
    public PollVotingRepository(DmMongoClient client, IMapper mapper) : base(client)
    {
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Poll> Vote(Guid pollId, Guid optionId, Guid userId)
    {
        var dbPoll = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(p => p.Id, pollId) &
            Filter.ElemMatch(p => p.Options, o => o.Id == optionId),
            Update.Push(u => u.Options.FirstMatchingElement().UserIds, userId),
            new FindOneAndUpdateOptions<DbPoll>
            {
                ReturnDocument = ReturnDocument.After
            });
        return mapper.Map<Poll>(dbPoll);
    }

    /// <inheritdoc />
    public async Task<Poll> Unvote(Guid pollId, Guid userId)
    {
        var dbPoll = await Collection.FindOneAndUpdateAsync(
            Filter.Eq(p => p.Id, pollId),
            Update.PullAll("Options.$[].UserIds", new[] { userId }),
            new FindOneAndUpdateOptions<DbPoll>
            {
                ReturnDocument = ReturnDocument.After
            });
        return mapper.Map<Poll>(dbPoll);
    }
}