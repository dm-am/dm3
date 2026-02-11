using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.Core.Dto.Enums;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Lifecycle;

/// <inheritdoc />
internal class GlobalChatEventLifecycleRepository : IGlobalChatEventLifecycleRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventLifecycleRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<GlobalChatEvent> UpdateStatus(
        Guid eventId,
        GlobalChatEventStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? endedAt,
        CancellationToken ct = default)
    {
        var GlobalChatEvent = await _dbContext.GlobalChatEvents
            .FirstAsync(e => e.GlobalChatEventId == eventId, ct).ConfigureAwait(false);

        GlobalChatEvent.Status = status;
        if (startedAt.HasValue)
        {
            GlobalChatEvent.StartedAtUtc = startedAt.Value;
        }
        if (endedAt.HasValue)
        {
            GlobalChatEvent.EndedAtUtc = endedAt.Value;
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEvents
            .Where(e => e.GlobalChatEventId == eventId)
            .ProjectTo<GlobalChatEvent>(_mapper.ConfigurationProvider)
            .FirstAsync(ct).ConfigureAwait(false);
    }
}
