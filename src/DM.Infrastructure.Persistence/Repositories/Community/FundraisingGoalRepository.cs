using System;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Community.Features.Fundraising;
using Microsoft.EntityFrameworkCore;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class FundraisingGoalRepository : IFundraisingGoalRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public FundraisingGoalRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    // READ

    /// <inheritdoc />
    public async Task<FundraisingGoal?> Get()
    {
        return await _dbContext.FundraisingGoals
            .ProjectTo<FundraisingGoal>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    // WRITE

    /// <inheritdoc />
    public async Task<FundraisingGoal> Update(UpdateFundraisingGoal update, Guid updatedByUserId, DateTimeOffset updatedUtc)
    {
        var dbGoal = await _dbContext.FundraisingGoals.FirstOrDefaultAsync();
        if (dbGoal == null)
        {
            throw new InvalidOperationException("Fundraising goal not found");
        }

        dbGoal.GoalAmount = update.GoalAmount;
        dbGoal.CollectedAmount = update.CollectedAmount;
        dbGoal.UpdatedUtc = updatedUtc;
        dbGoal.UpdatedByUserId = updatedByUserId;

        await _dbContext.SaveChangesAsync();

        return _mapper.Map<FundraisingGoal>(dbGoal);
    }
}
