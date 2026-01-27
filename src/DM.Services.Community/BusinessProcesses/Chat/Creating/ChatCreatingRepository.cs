using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Services.Community.BusinessProcesses.Chat.Reading;
using DM.Services.DataAccess;
using DM.Services.DataAccess.BusinessObjects.Messaging;
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
    public async Task<ChatMessage> Create(Message message)
    {
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Messages
            .Where(m => m.MessageId == message.MessageId)
            .ProjectTo<ChatMessage>(_mapper.ConfigurationProvider)
            .FirstAsync();
    }
}
