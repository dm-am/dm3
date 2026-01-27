using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.Gaming.Dto.Output;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Gaming.BusinessProcesses.Votes.Reading;

/// <inheritdoc />
internal class VoteReadingRepository : IVoteReadingRepository
{
    private readonly DmDbContext dbContext;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public VoteReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        this.dbContext = dbContext;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Vote>> GetByPost(Guid postId)
    {
        return await dbContext.Votes
            .Where(v => v.PostId == postId)
            .OrderByDescending(v => v.CreateDate)
            .ProjectTo<Vote>(mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task<Vote> Get(Guid voteId)
    {
        return await dbContext.Votes
            .Where(v => v.VoteId == voteId)
            .ProjectTo<Vote>(mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<bool> HasVoted(Guid postId, Guid userId)
    {
        return await dbContext.Votes
            .AnyAsync(v => v.PostId == postId && v.UserId == userId);
    }
}
