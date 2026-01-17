using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace DM.Services.Community.BusinessProcesses.Chat.Creating;

/// <inheritdoc />
internal class ChatCreatingRepository : IChatCreatingRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public ChatCreatingRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ChatMessage> Create(DataAccess.BusinessObjects.Common.ChatMessage chatMessage)
    {
        _dbContext.ChatMessages.Add(chatMessage);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.ChatMessages
            .Where(m => m.ChatMessageId == chatMessage.ChatMessageId)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}