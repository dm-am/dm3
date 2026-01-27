using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using DalVote = DM.Services.DataAccess.BusinessObjects.Games.Rating.Vote;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Creating;

/// <inheritdoc />
internal class VoteCreatingRepository : IVoteCreatingRepository
{
    private readonly DmDbContext dbContext;

    /// <inheritdoc />
    public VoteCreatingRepository(DmDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<DalVote> Create(DalVote vote)
    {
        dbContext.Votes.Add(vote);
        await dbContext.SaveChangesAsync();

        return await dbContext.Votes
            .Include(v => v.VotedUser)
            .FirstAsync(v => v.VoteId == vote.VoteId);
    }
}
