using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Deleting;

/// <inheritdoc />
internal class GlobalChatEventDeletingRepository : IGlobalChatEventDeletingRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GlobalChatEventDeletingRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid eventId, CancellationToken ct = default)
    {
        // Delete participants first
        var participants = await _dbContext.GlobalChatEventParticipants
            .Where(p => p.GlobalChatEventId == eventId)
            .ToListAsync(ct).ConfigureAwait(false);
        _dbContext.GlobalChatEventParticipants.RemoveRange(participants);

        // Then delete the event
        var GlobalChatEvent = await _dbContext.GlobalChatEvents
            .FirstOrDefaultAsync(e => e.GlobalChatEventId == eventId, ct).ConfigureAwait(false);
        if (GlobalChatEvent != null)
        {
            _dbContext.GlobalChatEvents.Remove(GlobalChatEvent);
        }

        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
