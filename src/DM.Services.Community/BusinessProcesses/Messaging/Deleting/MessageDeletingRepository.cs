using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Messaging.Deleting;

/// <inheritdoc />
internal class MessageDeletingRepository : IMessageDeletingRepository
{
    private readonly DmDbContext dbContext;

    /// <inheritdoc />
    public MessageDeletingRepository(DmDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default)
    {
        await dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsRemoved, true)
                .SetProperty(m => m.DeletedByUserId, deletedByUserId)
                .SetProperty(m => m.DeletedAtUtc, DateTimeOffset.UtcNow), ct);
    }
}
