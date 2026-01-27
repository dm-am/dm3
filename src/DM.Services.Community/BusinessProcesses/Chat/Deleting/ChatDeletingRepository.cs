using System;
using System.Threading.Tasks;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Chat.Deleting;

/// <inheritdoc />
internal class ChatDeletingRepository : IChatDeletingRepository
{
    private readonly DmDbContext _dbContext;

    /// <inheritdoc />
    public ChatDeletingRepository(DmDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task Delete(Guid id)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == id && m.ConversationId == Message.GlobalChatId);

        if (message != null)
        {
            message.IsRemoved = true;
            await _dbContext.SaveChangesAsync();
        }
    }
}
