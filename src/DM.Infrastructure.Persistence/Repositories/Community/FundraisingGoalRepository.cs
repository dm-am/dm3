using System;
using System.Threading.Tasks;
using DM.Domain.Community.Features.Fundraising;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class FundraisingGoalRepository : IFundraisingGoalRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public FundraisingGoalRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    // READ

    /// <inheritdoc />
    public async Task<FundraisingGoal?> Get()
    {
        return await _dbContext.FundraisingGoals
            .ProjectToFundraisingGoal()
            .FirstOrDefaultAsync();
    }

    // WRITE

    /// <inheritdoc />
    public async Task<FundraisingGoal> Update(UpdateFundraisingGoal update, Guid updatedByUserId, DateTimeOffset modifiedUtc)
    {
        var dbGoal = await _dbContext.FundraisingGoals.FirstOrDefaultAsync();
        if (dbGoal == null)
        {
            throw new InvalidOperationException("Fundraising goal not found");
        }

        dbGoal.Title = update.Title;
        dbGoal.GoalAmount = update.GoalAmount;
        dbGoal.CollectedAmount = update.CollectedAmount;
        dbGoal.ModifiedUtc = modifiedUtc;
        dbGoal.UpdatedByUserId = updatedByUserId;

        await _dbContext.SaveChangesAsync();

        return dbGoal.ToFundraisingGoal();
    }
}
