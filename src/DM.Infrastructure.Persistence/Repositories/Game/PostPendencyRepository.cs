using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Enums;
using DM.Domain.Game.Features.Games;
using DM.Domain.Game.Features.PostPendencies;
using Microsoft.EntityFrameworkCore;
using DbPostPendency = DM.Infrastructure.Persistence.Entities.Game.Links.PostPendency;

namespace DM.Infrastructure.Persistence.Repositories.Game;

/// <inheritdoc cref="IPostPendencyRepository" />
internal class PostPendencyRepository : IPostPendencyRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PostPendencyRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<PostPendency?> Get(Guid pendencyId)
    {
        return _dbContext.PostPendencies
            .TagWith("DM.PostPendency.Get")
            .Where(e => e.PendencyId == pendencyId)
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync()!;
    }

    /// <inheritdoc />
    public async Task<PostPendency> Create(CreatePostPendencyEntity entity)
    {
        var dbEntity = new DbPostPendency
        {
            PendencyId = entity.PendencyId,
            RoomId = entity.RoomId,
            CharacterId = entity.CharacterId,
            WaitingForUserId = entity.WaitingForUserId,
            CreatedById = entity.CreatedById,
            CreatedUtc = entity.CreatedUtc
        };

        _dbContext.PostPendencies.Add(dbEntity);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.PostPendencies
            .TagWith("DM.PostPendency.Created")
            .Where(e => e.PendencyId == entity.PendencyId)
            .ProjectTo<PostPendency>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }

    /// <inheritdoc />
    public async Task Delete(Guid pendencyId)
    {
        var pendency = await _dbContext.PostPendencies.FindAsync(pendencyId);
        if (pendency != null)
        {
            _dbContext.PostPendencies.Remove(pendency);
            await _dbContext.SaveChangesAsync();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<Guid>> ClaimPendingReminders(
        DateTimeOffset createdBefore,
        DateTimeOffset lastReminderBefore,
        DateTimeOffset remindedUtc,
        CancellationToken cancellationToken = default)
    {
        var due = await _dbContext.PostPendencies
            .TagWith("DM.PostPendency.ClaimPendingReminders")
            .Where(p =>
                p.FulfilledUtc == null &&
                p.CreatedUtc < createdBefore &&
                (p.LastReminderUtc == null || p.LastReminderUtc < lastReminderBefore) &&
                p.Room.Game!.Status == ModuleStatus.Active)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        foreach (var pendency in due)
        {
            pendency.LastReminderUtc = remindedUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return due.Select(p => p.PendencyId).ToList();
    }
}
