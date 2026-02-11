using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;
using DbGlobalChatEventParticipant = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEventParticipant;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Creating;

/// <inheritdoc />
internal class GlobalChatEventCreatingRepository : IGlobalChatEventCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public GlobalChatEventCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Reading.GlobalChatEvent> Create(
        DbGlobalChatEvent GlobalChatEvent,
        DbGlobalChatEventParticipant creatorParticipant,
        CancellationToken ct = default)
    {
        _dbContext.GlobalChatEvents.Add(GlobalChatEvent);
        _dbContext.GlobalChatEventParticipants.Add(creatorParticipant);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return await _dbContext.GlobalChatEvents
            .Where(e => e.GlobalChatEventId == GlobalChatEvent.GlobalChatEventId)
            .ProjectTo<Reading.GlobalChatEvent>(_mapper.ConfigurationProvider)
            .FirstAsync(ct).ConfigureAwait(false);
    }
}
