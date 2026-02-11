using System;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Deleting;

/// <inheritdoc />
internal class VoteDeletingRepository : IVoteDeletingRepository
{
    private readonly DmDbContext dbContext;

    /// <inheritdoc />
    public VoteDeletingRepository(DmDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid voteId)
    {
        var vote = await dbContext.Votes.FirstOrDefaultAsync(v => v.VoteId == voteId);
        if (vote != null)
        {
            dbContext.Votes.Remove(vote);
            await dbContext.SaveChangesAsync();
        }
    }
}
