using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
using Microsoft.EntityFrameworkCore;
using ChatMessageDto = DM.Services.Community.BusinessProcesses.Chat.Reading.ChatMessage;

namespace DM.Services.Community.BusinessProcesses.Chat.Updating;

/// <inheritdoc />
internal class ChatUpdatingRepository : IChatUpdatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatUpdatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ChatMessageDto> Update(Guid id, string text, Guid editorUserId)
    {
        var message = await _dbContext.Messages
            .FirstOrDefaultAsync(m => m.MessageId == id && m.ConversationId == Message.GlobalChatId);

        if (message == null) return null;

        message.Text = text;
        message.ModifiedUtc = DateTimeOffset.UtcNow;
        message.ModifiedByUserId = editorUserId;

        // Create edit history record
        var editRecord = new MessageEdit
        {
            MessageEditId = Guid.NewGuid(),
            MessageId = id,
            EditorUserId = editorUserId,
            EditedAtUtc = DateTimeOffset.UtcNow
        };
        _dbContext.MessageEdits.Add(editRecord);

        await _dbContext.SaveChangesAsync();

        return await _dbContext.Messages
            .Where(m => m.MessageId == id)
            .ProjectTo<ChatMessageDto>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}
