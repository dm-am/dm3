using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Reading;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using DbParticipant = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEventParticipant;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Participants;

/// <inheritdoc />
internal class GlobalChatEventParticipantRepository : IGlobalChatEventParticipantRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventParticipantRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GlobalChatEventParticipant>> GetParticipants(Guid eventId) =>
        await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventId == eventId)
            .OrderBy(p => p.JoinedAtUtc)
            .ProjectTo<GlobalChatEventParticipant>(_mapper.ConfigurationProvider)
            .ToArrayAsync().ConfigureAwait(false);

    /// <inheritdoc />
    public async Task<GlobalChatEventParticipant> Add(
        DbParticipant participant,
        CancellationToken ct = default)
    {
        _dbContext.GlobalChatEventParticipants.Add(participant);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventParticipantId == participant.GlobalChatEventParticipantId)
            .ProjectTo<GlobalChatEventParticipant>(_mapper.ConfigurationProvider)
            .FirstAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task Remove(Guid eventId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _dbContext.GlobalChatEventParticipants
            .FirstOrDefaultAsync(p => p.GlobalChatEventId == eventId && p.UserId == userId, ct).ConfigureAwait(false);

        if (participant != null)
        {
            _dbContext.GlobalChatEventParticipants.Remove(participant);
            await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> IsParticipant(Guid eventId, Guid userId) =>
        _dbContext.GlobalChatEventParticipants
            .AnyAsync(p => p.GlobalChatEventId == eventId && p.UserId == userId);
}
