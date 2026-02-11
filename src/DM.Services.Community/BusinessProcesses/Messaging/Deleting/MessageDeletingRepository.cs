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
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public MessageDeletingRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid messageId, Guid deletedByUserId, CancellationToken ct = default)
    {
        // Get conversation info before deletion
        var messageInfo = await _dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .Select(m => new { m.ConversationId, m.Conversation.LastMessageId })
            .FirstOrDefaultAsync(ct);

        // Mark message as removed
        await _dbContext.Messages
            .Where(m => m.MessageId == messageId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsRemoved, true)
                .SetProperty(m => m.DeletedByUserId, deletedByUserId)
                .SetProperty(m => m.DeletedAtUtc, DateTimeOffset.UtcNow), ct);

        // If this was the last message in conversation, update LastMessageId
        if (messageInfo?.LastMessageId == messageId)
        {
            var newLastMessageId = await _dbContext.Messages
                .Where(m => m.ConversationId == messageInfo.ConversationId)
                .OrderByDescending(m => m.CreatedUtc)
                .Select(m => (Guid?)m.MessageId)
                .FirstOrDefaultAsync(ct);

            await _dbContext.Conversations
                .Where(c => c.ConversationId == messageInfo.ConversationId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.LastMessageId, newLastMessageId), ct);
        }
    }
}
