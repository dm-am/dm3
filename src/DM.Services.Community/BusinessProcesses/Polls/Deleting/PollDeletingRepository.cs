using System;
using System.Threading.Tasks;
using DM.Services.DataAccess.MongoIntegration;
using MongoDB.Driver;
using Poll = DM.Services.DataAccess.BusinessObjects.Boards.Poll;

namespace DM.Services.Community.BusinessProcesses.Polls.Deleting;

/// <inheritdoc />
internal class PollDeletingRepository : MongoCollectionRepository<Poll>, IPollDeletingRepository
{
    /// <inheritdoc />
    public PollDeletingRepository(DmMongoClient client) : base(client)
    {
    }

    /// <inheritdoc />
    public Task Delete(Guid pollId) =>
        Collection.UpdateOneAsync(
            Filter.Eq(p => p.Id, pollId),
            Update.Set(p => p.IsRemoved, true));
}
