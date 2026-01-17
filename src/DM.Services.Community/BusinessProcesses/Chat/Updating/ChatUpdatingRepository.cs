using System;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

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
    public async Task<ChatMessage> Update(Guid id, string text)
    {
        var message = await _dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.ChatMessageId == id);

        if (message == null) return null;

        message.Text = text;
        message.LastUpdateDate = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync();

        return await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == id)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstOrDefaultAsync();
    }
}
