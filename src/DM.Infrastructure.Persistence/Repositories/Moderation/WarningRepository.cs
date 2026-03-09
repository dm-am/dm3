using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Moderation.Features.Warnings;
using Microsoft.EntityFrameworkCore;
using DbWarning = DM.Infrastructure.Persistence.Entities.Moderation.Warning;

namespace DM.Infrastructure.Persistence.Repositories.Moderation;

/// <inheritdoc />
internal class WarningRepository : IWarningRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public WarningRepository(DmDbContext dbContext, IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Warning>> GetUserWarnings(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Where(w => w.TargetUserId == userId && !w.IsRemoved)
            .OrderByDescending(w => w.CreatedUtc)
            .ProjectTo<Warning>(_mapper.ConfigurationProvider)
            .ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Warning?> Get(Guid warningId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Where(w => w.WarningId == warningId)
            .ProjectTo<Warning>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Warning> Create(CreateWarningEntity entity, CancellationToken ct = default)
    {
        var warning = new DbWarning
        {
            WarningId = entity.WarningId,
            TargetUserId = entity.TargetUserId,
            AuthorId = entity.AuthorId,
            EntityId = entity.EntityId,
            EntityType = entity.EntityType,
            Points = entity.Points,
            Text = entity.Text,
            CreatedUtc = entity.CreatedUtc,
            IsRemoved = false
        };

        _dbContext.Warnings.Add(warning);
        await _dbContext.SaveChangesAsync(ct);

        return await Get(warning.WarningId, ct) ?? throw new InvalidOperationException("Failed to retrieve created warning");
    }

    /// <inheritdoc />
    public async Task Remove(Guid warningId, CancellationToken ct = default)
    {
        var warning = await _dbContext.Warnings.FindAsync(new object[] { warningId }, ct);
        if (warning != null)
        {
            warning.IsRemoved = true;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    /// <inheritdoc />
    public async Task<int> GetUserWarningPoints(Guid userId, CancellationToken ct = default)
    {
        return await _dbContext.Warnings
            .Where(w => w.TargetUserId == userId && !w.IsRemoved)
            .SumAsync(w => w.Points, ct);
    }
}
