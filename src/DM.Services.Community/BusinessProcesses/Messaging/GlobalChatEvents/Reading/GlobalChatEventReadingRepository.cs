using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;

/// <inheritdoc />
internal class GlobalChatEventReadingRepository : IGlobalChatEventReadingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventReadingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public Task<GlobalChatEvent?> Get(Guid eventId) => _dbContext.GlobalChatEvents
        .Where(e => e.GlobalChatEventId == eventId)
        .ProjectTo<GlobalChatEvent>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GlobalChatEvent>> GetByStatus(params GlobalChatEventStatus[] statuses) =>
        await _dbContext.GlobalChatEvents
            .Where(e => statuses.Contains(e.Status))
            .OrderByDescending(e => e.StartsAtUtc)
            .ProjectTo<GlobalChatEvent>(_mapper.ConfigurationProvider)
            .ToArrayAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public Task<GlobalChatEvent?> GetActiveEvent() => _dbContext.GlobalChatEvents
        .Where(e => e.Status == GlobalChatEventStatus.Live)
        .ProjectTo<GlobalChatEvent>(_mapper.ConfigurationProvider)
        .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<IEnumerable<GlobalChatEvent>> GetUpcomingEvents() =>
        await _dbContext.GlobalChatEvents
            .Where(e => e.Status == GlobalChatEventStatus.Scheduled)
            .OrderBy(e => e.StartsAtUtc)
            .ProjectTo<GlobalChatEvent>(_mapper.ConfigurationProvider)
            .ToArrayAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public Task<bool> HasActiveEvent() => _dbContext.GlobalChatEvents
        .AnyAsync(e => e.Status == GlobalChatEventStatus.Live);

    /// <inheritdoc />
    public Task<bool> IsParticipant(Guid eventId, Guid userId) =>
        _dbContext.GlobalChatEventParticipants
            .AnyAsync(p => p.GlobalChatEventId == eventId && p.UserId == userId);
}
