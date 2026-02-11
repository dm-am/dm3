using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.RelationalStorage;
using DbGlobalChatEvent = DM.Services.DataAccess.BusinessObjects.Messaging.GlobalChatEvent;

namespace DM.Services.Community.BusinessProcesses.Messaging.GlobalChatEvents.Updating;

/// <inheritdoc />
internal class GlobalChatEventUpdatingRepository : IGlobalChatEventUpdatingRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public GlobalChatEventUpdatingRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Update(IUpdateBuilder<DbGlobalChatEvent> updateBuilder, CancellationToken ct = default)
    {
        updateBuilder.AttachTo(_dbContext);
        await _dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
